using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace vietnamgiapha.AI
{
    public sealed partial class GiaPhaQueryEngine
    {
        // ── Parse intent (rule) ───────────────────────────────────────────────

        /// <summary>Parse câu hỏi bằng rule — không gọi LLM.</summary>
        public GiaPhaParseResult TryParseRule(string question, FamilyViewModel currentFamily)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return FailParse(question, "Câu hỏi trống.");
            }

            string q = RemoveDiacritics(question.Trim().ToLowerInvariant());

            // Chào hỏi / stopword — không coi là tên Search; để rule fail → Qwen hoặc gợi ý hỏi lại
            if (GiaPhaRuleEngineLoader.MatchesStopword(q))
            {
                return FailParse(question, "stopword");
            }

            string normalizedForm = TryNormalizeAiLaQuestion(question, q);
            if (normalizedForm != null)
            {
                return TryParseRule(normalizedForm, currentFamily);
            }

            // Lệnh sửa gia phả — ưu tiên trước tra cứu (vd. "thêm người vào gia đình")
            GiaPhaParseResult editRule = TryMatchEditActionRules(q, question);
            if (editRule != null)
            {
                return editRule;
            }

            GiaPhaParseResult topRule = TryMatchTopIntentRules(q, question);
            if (topRule != null)
            {
                return topRule;
            }

            var mNamSinh = Regex.Match(q, @"sinh\s*(?:nam)?\s*(\d{4})");
            if (mNamSinh.Success && int.TryParse(mNamSinh.Groups[1].Value, out int namSinh))
            {
                return RuleParse(new GiaPhaIntent
                {
                    Kind = GiaPhaIntentKinds.ByBirthYear,
                    Year = namSinh,
                    IsBirthYear = true,
                    RawQuestion = question
                });
            }

            var mNamMat = Regex.Match(q, @"(?:mat|chet|qua doi|tu tran|ky)\s*(?:nam)?\s*(\d{4})");
            if (mNamMat.Success && int.TryParse(mNamMat.Groups[1].Value, out int namMat))
            {
                return RuleParse(new GiaPhaIntent
                {
                    Kind = GiaPhaIntentKinds.ByDeathYear,
                    Year = namMat,
                    IsBirthYear = false,
                    RawQuestion = question
                });
            }

            var matchDoi = Regex.Match(q, @"doi\s*(\d+)");
            if (matchDoi.Success && int.TryParse(matchDoi.Groups[1].Value, out int level))
            {
                string afterDoi = q.Substring(matchDoi.Index + matchDoi.Length).Trim();
                if (string.IsNullOrWhiteSpace(afterDoi)
                    || afterDoi.StartsWith("co ai") || afterDoi.StartsWith("co gi")
                    || afterDoi.StartsWith("co bao nhieu") || afterDoi.StartsWith("bao nhieu")
                    || afterDoi.StartsWith("co may") || afterDoi.StartsWith("may nguoi"))
                {
                    return RuleParse(new GiaPhaIntent
                    {
                        Kind = GiaPhaIntentKinds.GenerationList,
                        Generation = level,
                        RawQuestion = question
                    });
                }
            }

            string nameA;
            string nameB;
            if (TryExtractTwoNames(question, out nameA, out nameB))
            {
                return RuleParse(new GiaPhaIntent
                {
                    Kind = GiaPhaIntentKinds.Relationship,
                    Names = new List<string> { nameA, nameB },
                    RawQuestion = question
                });
            }

            string detailKind = MapDetailKindFromQuestion(q, out int ancestorLevels, out string spouseRole);
            string name = ExtractName(question);
            string nameNorm = RemoveDiacritics(name.ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(nameNorm) || detailKind != GiaPhaIntentKinds.Unknown)
            {
                string kind = detailKind;
                if (kind == GiaPhaIntentKinds.Unknown)
                {
                    // Một cụm lẻ không phải intent chi tiết → Search (vd. "Nghĩa", "Lan")
                    if (GiaPhaRuleEngineLoader.MatchesStopword(nameNorm))
                    {
                        return FailParse(question, null);
                    }

                    kind = GiaPhaIntentKinds.Search;
                }

                var intent = new GiaPhaIntent
                {
                    Kind = kind,
                    RawQuestion = question,
                    AncestorLevels = ancestorLevels,
                    SpouseRole = spouseRole
                };
                if (!string.IsNullOrWhiteSpace(name))
                {
                    intent.Names.Add(name);
                }

                bool strict = IsStrictRuleParse(question, q, name, nameNorm, kind);
                return RuleParse(intent, strict);
            }

            return FailParse(question, null);
        }

        /// <summary>Rule weak khi tách tên kém (cả câu làm tên) — để orchestrator gọi Qwen.</summary>
        private static bool IsStrictRuleParse(
            string question,
            string q,
            string name,
            string nameNorm,
            string kind)
        {
            if (string.IsNullOrWhiteSpace(nameNorm))
            {
                // Intent đã có kind chi tiết nhưng không tách được tên
                return kind == GiaPhaIntentKinds.Unknown;
            }

            string qFull = RemoveDiacritics(question.Trim().ToLowerInvariant());

            // Một từ lẻ: "Nghĩa", "Lan"
            if (!question.Trim().Contains(" "))
            {
                return true;
            }

            // Tên chiếm gần hết câu → weak (vd. "tôi muốn tìm ông Nghĩa")
            if (nameNorm.Length >= qFull.Length * 0.75)
            {
                return false;
            }

            if (!qFull.Contains(nameNorm))
            {
                return false;
            }

            return true;
        }

        // ── Execute intent → fact ─────────────────────────────────────────────

        /// <summary>Thực thi intent trên index — mọi câu trả lời là fact từ engine.</summary>
        public GiaPhaQueryResult Execute(
            GiaPhaIntent intent,
            FamilyViewModel currentFamily,
            GiaPhaParseSource parseSource = GiaPhaParseSource.Rule)
        {
            if (intent == null)
            {
                return GiaPhaQueryResult.Fail("Không có intent.");
            }

            if (!IsReady)
            {
                return GiaPhaQueryResult.Fail("⚠️ Chưa có dữ liệu gia phả. Hãy mở file gia phả trước.");
            }

            if (intent.Kind == GiaPhaIntentKinds.Unknown)
            {
                return GiaPhaQueryResult.Fail(GetParseHelpMessage());
            }

            string answer;
            try
            {
                answer = ExecuteCore(intent, currentFamily);
            }
            catch (Exception ex)
            {
                return GiaPhaQueryResult.Fail("❌ Lỗi tra cứu: " + ex.Message);
            }

            return new GiaPhaQueryResult
            {
                Success = true,
                AnswerText = answer,
                FactsPack = GiaPhaFactsBuilder.Build(intent, answer, currentFamily),
                Intent = intent,
                ParseSource = parseSource,
                StatusHint = "Fact từ engine"
            };
        }

        private string ExecuteCore(GiaPhaIntent intent, FamilyViewModel currentFamily)
        {
            switch (intent.Kind)
            {
                case GiaPhaIntentKinds.ThuyTo:
                    return QueryThuyTo();
                case GiaPhaIntentKinds.CountPeople:
                    return QuerySoNguoi();
                case GiaPhaIntentKinds.NoDescendants:
                    return QueryChuaCoCon();
                case GiaPhaIntentKinds.ByBirthYear:
                    return QueryTheoNam(intent.Year ?? 0, true);
                case GiaPhaIntentKinds.ByDeathYear:
                    return QueryTheoNam(intent.Year ?? 0, false);
                case GiaPhaIntentKinds.GenerationList:
                    return QueryDoiSo(intent.Generation ?? 0);
                case GiaPhaIntentKinds.CurrentFamily:
                    return QueryCurrentFamily(
                        currentFamily,
                        RemoveDiacritics((intent.RawQuestion ?? "").ToLowerInvariant()));
                case GiaPhaIntentKinds.Relationship:
                    if (intent.Names.Count >= 2)
                    {
                        return QueryRelationship(intent.Names[0], intent.Names[1]);
                    }
                    break;
            }

            return ExecuteNamedDetail(intent, currentFamily);
        }

        private string ExecuteNamedDetail(GiaPhaIntent intent, FamilyViewModel currentFamily)
        {
            string q = RemoveDiacritics((intent.RawQuestion ?? "").ToLowerInvariant());
            string name = intent.Names.Count > 0 ? intent.Names[0] : ExtractName(intent.RawQuestion ?? "");
            string nameNorm = RemoveDiacritics(name.ToLowerInvariant());

            if (string.IsNullOrWhiteSpace(nameNorm))
            {
                return GetParseHelpMessage();
            }

            var entries = FindByName(nameNorm);
            if (entries.Count == 0)
            {
                return "Không tìm thấy ai tên \"" + name + "\" trong gia phả.\n"
                    + "Hãy kiểm tra lại tên hoặc thử gõ một phần tên họ.";
            }

            Func<SearchEntry, string> detailFunc = GetDetailFuncForIntent(intent, q);

            if (entries.Count == 1)
            {
                return detailFunc != null
                    ? detailFunc(entries[0])
                    : QueryThongTinDay(entries[0]);
            }

            if (detailFunc == null)
            {
                return FormatMultipleResults(name, entries);
            }

            return QueryDetailForAll(name, entries, detailFunc);
        }

        private Func<SearchEntry, string> GetDetailFuncForIntent(GiaPhaIntent intent, string qNormalized)
        {
            if (intent.Kind == GiaPhaIntentKinds.PersonSummary || intent.Kind == GiaPhaIntentKinds.Search)
            {
                return null;
            }

            Func<SearchEntry, string> fromKind = GetDetailFuncForKind(
                intent.Kind,
                intent.AncestorLevels ?? 0,
                intent.SpouseRole);
            if (fromKind != null)
            {
                return fromKind;
            }

            return GetDetailQueryFunc(qNormalized);
        }

        /// <summary>Khớp lệnh sửa từ ai/rules/edit-actions.txt.</summary>
        private GiaPhaParseResult TryMatchEditActionRules(string q, string question)
        {
            foreach (GiaPhaRuleKeywordEntry rule in GiaPhaRuleEngineLoader.GetSnapshot().EditIntentRules)
            {
                if (!ContainsAny(q, rule.Keywords))
                {
                    continue;
                }

                var intent = new GiaPhaIntent
                {
                    Kind = rule.Kind,
                    RawQuestion = question,
                    UseCurrentFamily = rule.UseCurrentFamily
                };

                TryFillEditIntentFields(intent, question, q);
                return RuleParse(intent, true);
            }

            return null;
        }

        /// <summary>Tách tên người / tên mới cho lệnh biên tập.</summary>
        private void TryFillEditIntentFields(GiaPhaIntent intent, string question, string q)
        {
            if (intent == null)
            {
                return;
            }

            // "thêm người tên X" / "them nguoi ten X"
            var mTen = Regex.Match(q, @"(?:ten|tên)\s+(.+)$");
            if (mTen.Success)
            {
                string extracted = mTen.Groups[1].Value.Trim();
                extracted = TrimEditTail(extracted);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    intent.NewPersonName = RestoreNameCase(question, extracted);
                }
            }

            // "đổi tên A thành B"
            var mRename = Regex.Match(
                q,
                @"(?:doi|sua)\s+ten(?:\s+cua)?\s+(.+?)\s+(?:thanh|than)\s+(.+)");
            if (mRename.Success)
            {
                string oldPart = mRename.Groups[1].Value.Trim();
                string newPart = TrimEditTail(mRename.Groups[2].Value.Trim());
                if (!string.IsNullOrWhiteSpace(oldPart))
                {
                    intent.Names.Add(RestoreNameCase(question, oldPart));
                }

                if (!string.IsNullOrWhiteSpace(newPart))
                {
                    intent.NewPersonName = RestoreNameCase(question, newPart);
                }

                return;
            }

            // "thêm người vào gia đình của X"
            string[] familyOfPrefixes = {
                "them nguoi vao gia dinh cua ",
                "them nguoi vao gia dinh ",
                "them thanh vien vao gia dinh cua ",
                "them thanh vien vao gia dinh "
            };
            foreach (string pre in familyOfPrefixes)
            {
                if (!q.StartsWith(pre))
                {
                    continue;
                }

                string namePart = q.Substring(pre.Length).Trim();
                namePart = TrimEditTail(namePart);
                if (!string.IsNullOrWhiteSpace(namePart)
                    && !ContainsAny(namePart, "nay", "dang chon", "hien tai"))
                {
                    intent.Names.Add(RestoreNameCase(question, namePart));
                    intent.UseCurrentFamily = false;
                }

                return;
            }

            if (ContainsAny(q, "gia dinh nay", "dang chon", "hien tai", "dang xem"))
            {
                intent.UseCurrentFamily = true;
            }
        }

        private static string TrimEditTail(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            string[] tails = { " vao gia dinh", " vao gia pha", " giup toi", " giup minh", " nhe", " a" };
            string t = text.Trim();
            foreach (string tail in tails)
            {
                if (t.EndsWith(tail))
                {
                    t = t.Substring(0, t.Length - tail.Length).Trim();
                }
            }

            return t;
        }

        /// <summary>Lấy cụm tên từ câu gốc (giữ dấu) theo bản không dấu đã tách.</summary>
        private static string RestoreNameCase(string originalQuestion, string normalizedPart)
        {
            if (string.IsNullOrWhiteSpace(originalQuestion) || string.IsNullOrWhiteSpace(normalizedPart))
            {
                return normalizedPart;
            }

            string lowerOrig = originalQuestion.ToLowerInvariant();
            int idx = lowerOrig.IndexOf(normalizedPart, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return normalizedPart;
            }

            return originalQuestion.Substring(idx, normalizedPart.Length).Trim();
        }

        /// <summary>Khớp intent cấp cao từ ai/rules/top-intent.txt.</summary>
        private static GiaPhaParseResult TryMatchTopIntentRules(string q, string question)
        {
            foreach (GiaPhaRuleKeywordEntry rule in GiaPhaRuleEngineLoader.GetSnapshot().TopIntentRules)
            {
                if (!ContainsAny(q, rule.Keywords))
                {
                    continue;
                }

                var intent = new GiaPhaIntent
                {
                    Kind = rule.Kind,
                    RawQuestion = question,
                    UseCurrentFamily = rule.UseCurrentFamily
                };
                return RuleParse(intent);
            }

            return null;
        }

        /// <summary>Map câu hỏi → kind chi tiết từ ai/rules/detail-intent.txt.</summary>
        private static string MapDetailKindFromQuestion(
            string q,
            out int ancestorLevels,
            out string spouseRole)
        {
            ancestorLevels = 0;
            spouseRole = null;

            foreach (GiaPhaRuleKeywordEntry rule in GiaPhaRuleEngineLoader.GetSnapshot().DetailIntentRules)
            {
                if (!ContainsAny(q, rule.Keywords))
                {
                    continue;
                }

                if (rule.AncestorLevels.HasValue)
                {
                    ancestorLevels = rule.AncestorLevels.Value;
                }

                if (!string.IsNullOrWhiteSpace(rule.SpouseRole))
                {
                    spouseRole = rule.SpouseRole;
                }

                return rule.Kind;
            }

            return GiaPhaIntentKinds.Unknown;
        }

        private static GiaPhaParseResult RuleParse(GiaPhaIntent intent, bool isStrict = true)
        {
            return new GiaPhaParseResult
            {
                Intent = intent,
                Source = GiaPhaParseSource.Rule,
                Note = isStrict ? "rule-strict" : "rule-weak",
                IsStrict = isStrict
            };
        }

        private static GiaPhaParseResult FailParse(string question, string note)
        {
            return new GiaPhaParseResult
            {
                Intent = GiaPhaIntent.Unknown(question),
                Source = GiaPhaParseSource.Failed,
                Note = note,
                IsStrict = false
            };
        }

        private static string GetParseHelpMessage()
        {
            return "Xin hỏi cụ thể hơn, ví dụ:\n"
                + "• \"Con của Nguyễn Văn Chính\"\n"
                + "• \"Mộ của Trần Thị Thê ở đâu?\"\n"
                + "• \"Đời 4 có ai?\"\n"
                + "• \"Lương Văn A và Trần Thị B có quan hệ gì?\"";
        }
    }
}

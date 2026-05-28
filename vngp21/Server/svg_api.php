<?php
/**
 * API kho SVG — một file PHP duy nhất (MySQL).
 *
 * Bảng: svg_data
 *   id, svg_category, svg_name, svg_author, svg_data, count_download, update_date
 *
 * action: list | get&id= | record_download&id= | check_duplicate (POST svg_data) | upload (POST) | categories
 */

header('Content-Type: application/json; charset=utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

$dbHost = 'localhost';
$dbName = 'vietnamgiapha';
$dbUser = 'root';
$dbPass = '';

$table = 'svg_data';

$action = isset($_REQUEST['action']) ? trim($_REQUEST['action']) : 'list';

try {
    $pdo = new PDO(
        "mysql:host={$dbHost};dbname={$dbName};charset=utf8mb4",
        $dbUser,
        $dbPass,
        array(
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        )
    );
} catch (Exception $e) {
    echo json_encode(array('code' => 1, 'msg' => 'DB: ' . $e->getMessage(), 'data' => null));
    exit;
}

function ok($data)
{
    echo json_encode(array('code' => 0, 'msg' => 'OK', 'data' => $data));
    exit;
}

function fail($msg, $code = 1, $data = null)
{
    echo json_encode(array('code' => $code, 'msg' => $msg, 'data' => $data));
    exit;
}

/** Chuẩn hóa một dòng DB sang JSON API (tên cột = tên field). */
function mapListRow($r)
{
    return array(
        'id' => (int)$r['id'],
        'svg_category' => $r['svg_category'],
        'svg_name' => $r['svg_name'],
        'svg_author' => $r['svg_author'],
        'count_download' => (int)$r['count_download'],
        'update_date' => $r['update_date'],
    );
}

function mapDetailRow($r)
{
    $row = mapListRow($r);
    $row['svg_data'] = $r['svg_data'];
    return $row;
}

/** Tìm khung có nội dung SVG trùng (so SHA-256 nội dung). */
function findDuplicateBySvgData($pdo, $table, $svgBody)
{
    $stmt = $pdo->prepare(
        "SELECT id, svg_category, svg_name, svg_author
         FROM {$table}
         WHERE SHA2(svg_data, 256) = SHA2(?, 256)
         LIMIT 1"
    );
    $stmt->execute(array($svgBody));
    $row = $stmt->fetch();
    return $row ? $row : null;
}

function failDuplicate($existing)
{
    $name = $existing['svg_name'];
    $cat = $existing['svg_category'];
    $author = isset($existing['svg_author']) ? trim($existing['svg_author']) : '';
    $id = (int)$existing['id'];
    $authorLine = $author !== '' ? "\n  • Tác giả: " . $author : '';

    $msg = "Khung SVG này đã có trên kho cloud (nội dung giống hệt).\n\n"
        . "Khung đang trùng:\n"
        . "  • Tên: " . $name . "\n"
        . "  • Nhóm (category): " . $cat . $authorLine . "\n"
        . "  • Mã trên cloud: id=" . $id . "\n\n"
        . "Không thể upload bản trùng. Bạn có thể:\n"
        . "  – Sửa file SVG cho khác, hoặc\n"
        . "  – Chọn khung đó trên cây «Kho cloud» bên trái, hoặc bấm «→ Thêm vào kho local».";

    fail($msg, 2, mapListRow($existing));
}

switch ($action) {
    case 'list':
        $stmt = $pdo->query(
            "SELECT id, svg_category, svg_name, svg_author, count_download, update_date
             FROM {$table}
             ORDER BY svg_category ASC, svg_name ASC"
        );
        $rows = $stmt->fetchAll();
        $out = array();
        foreach ($rows as $r) {
            $out[] = mapListRow($r);
        }
        ok($out);
        break;

    case 'categories':
        $stmt = $pdo->query("SELECT DISTINCT svg_category FROM {$table} ORDER BY svg_category ASC");
        $cats = array();
        while ($row = $stmt->fetch()) {
            $cats[] = $row['svg_category'];
        }
        ok($cats);
        break;

    case 'get':
        $id = isset($_REQUEST['id']) ? (int)$_REQUEST['id'] : 0;
        if ($id <= 0) {
            fail('Thiếu id');
        }
        $stmt = $pdo->prepare(
            "SELECT id, svg_category, svg_name, svg_author, svg_data, count_download, update_date
             FROM {$table} WHERE id = ? LIMIT 1"
        );
        $stmt->execute(array($id));
        $row = $stmt->fetch();
        if (!$row) {
            fail('Không tìm thấy SVG id=' . $id);
        }
        ok(mapDetailRow($row));
        break;

    case 'record_download':
        $id = isset($_REQUEST['id']) ? (int)$_REQUEST['id'] : 0;
        if ($id <= 0) {
            fail('Thiếu id');
        }
        $pdo->prepare("UPDATE {$table} SET count_download = count_download + 1 WHERE id = ?")->execute(array($id));
        $stmt = $pdo->prepare(
            "SELECT id, svg_category, svg_name, svg_author, count_download, update_date
             FROM {$table} WHERE id = ? LIMIT 1"
        );
        $stmt->execute(array($id));
        $row = $stmt->fetch();
        if (!$row) {
            fail('Không tìm thấy SVG id=' . $id);
        }
        ok(mapListRow($row));
        break;

    case 'check_duplicate':
        $svgBody = isset($_POST['svg_data']) ? $_POST['svg_data'] : '';
        if ($svgBody === '' && isset($_REQUEST['svg_data'])) {
            $svgBody = $_REQUEST['svg_data'];
        }
        if ($svgBody === '') {
            fail('Thiếu svg_data');
        }
        $dup = findDuplicateBySvgData($pdo, $table, $svgBody);
        if ($dup) {
            failDuplicate($dup);
        }
        ok(array('duplicate' => false));
        break;

    case 'upload':
        $category = isset($_POST['svg_category']) ? trim($_POST['svg_category']) : '';
        if ($category === '' && isset($_POST['category'])) {
            $category = trim($_POST['category']);
        }
        $name = isset($_POST['svg_name']) ? trim($_POST['svg_name']) : '';
        if ($name === '' && isset($_POST['name'])) {
            $name = trim($_POST['name']);
        }
        $author = isset($_POST['svg_author']) ? trim($_POST['svg_author']) : '';
        if ($author === '' && isset($_POST['author'])) {
            $author = trim($_POST['author']);
        }
        $svgBody = isset($_POST['svg_data']) ? $_POST['svg_data'] : '';

        if ($category === '') {
            $category = 'Chung';
        }
        if ($name === '') {
            fail('Thiếu svg_name');
        }
        if ($svgBody === '') {
            fail('Thiếu svg_data');
        }

        $dup = findDuplicateBySvgData($pdo, $table, $svgBody);
        if ($dup) {
            failDuplicate($dup);
        }

        $stmt = $pdo->prepare(
            "INSERT INTO {$table} (svg_category, svg_name, svg_author, svg_data, count_download, update_date)
             VALUES (?, ?, ?, ?, 0, NOW())"
        );
        $stmt->execute(array($category, $name, $author, $svgBody));
        $newId = (int)$pdo->lastInsertId();
        ok(array(
            'id' => $newId,
            'svg_category' => $category,
            'svg_name' => $name,
            'svg_author' => $author,
            'count_download' => 0,
        ));
        break;

    default:
        fail('action không hợp lệ: ' . $action);
}

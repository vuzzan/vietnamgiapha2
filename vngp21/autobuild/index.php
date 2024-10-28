<?php
//print_r( $_POST );
//print_r( $_FILES );
// Check if image file is a actual image or fake image
if(isset($_POST["version"]) && strlen($_POST["version"])>3 ) {
	$target_dir = "./"."VietNamGiaPha2_".trim($_POST["version"]).".zip";
	$name = basename($_FILES["file"]["name"]);
	move_uploaded_file($_FILES["file"]["tmp_name"], $target_dir);
	echo("Upload $target_dir<br>");
	//$update_xml = './autoupdate.tmp';
	$update_txt = file_get_contents('./autoupdate.tmp', true);
	$update_txt = str_replace( "[version]", trim($_POST["version"]), $update_txt );
	file_put_contents('./autoupdate.xml', $update_txt);
	echo($update_txt);
}
else{
	echo("NO UPDLOAD <br>");
}
/*
<!DOCTYPE html>
<html>
<body>

<form action="index.php" method="post" enctype="multipart/form-data">
  Select image to upload:
  <input type="text" name="version" value="1.2.3.0">
  <input type="file" name="file" ><br>
  <input type="submit" value="Upload" name="submit">
</form>

</body>
</html>

*/
?>

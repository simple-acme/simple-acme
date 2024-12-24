if ($env:APPVEYOR_REPO_TAG) {
	Compress $Out "$Bundle\signingbundle.zip"
	Status "Signing bundle created!"
} else {
	Status "Signing bundle skipped"
}
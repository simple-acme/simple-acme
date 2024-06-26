rm -r debug
mkdir debug
unzip -o ./artifacts/simple-acme.v2.0.0.0.linux-x64.trimmed.zip -d debug
sudo ./debug/wacs --source manual --host linux.wouter.tinus.online --store pemfiles --pemfilespath "\"/mnt/i\"" --installation script --script "\"/mnt/i/script.ps1\"" --scriptparameters "bla" --verbose --test --accepttos
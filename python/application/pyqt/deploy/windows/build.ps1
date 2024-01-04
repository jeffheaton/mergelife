if (-not $env:version) {
    Write-Host "Error: Environment variable version is not set."
    exit 1
}

# Environment
Set-Location ../..
Remove-Item -Recurse -Force ./venv -ErrorAction SilentlyContinue
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
Set-Location deploy/windows

# Build it
Remove-Item -Recurse -Force ./working -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path ./working | Out-Null
# Copy-Item ./heaton_ca_icon.icns ./working
Copy-Item ./heaton-ca-windows.spec ./working
Copy-Item ../../*.py ./working
Copy-Item heaton_ca_icon.png ./working/heaton_ca_icon.png
Copy-Item -Recurse ../../data ./working
Copy-Item -Recurse ../../mergelife ./working
Copy-Item -Recurse ../../jth_ui ./working

Set-Location ./working

Write-Host "** Pyinstaller **"
pyinstaller --clean --noconfirm --distpath dist --workpath build heaton-ca-windows.spec

cd ..
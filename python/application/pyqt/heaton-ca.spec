# -*- mode: python ; coding: utf-8 -*-


block_cipher = None

added_files = [('data/', 'data')]

a = Analysis(
    ['heaton-ca.py'],
    pathex=[],
    binaries=[],
    datas=added_files,
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False
)

pyz = PYZ(
    a.pure,
    a.zipped_data,
    cipher=block_cipher
)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='heaton-ca',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    target_arch='arm64', # universal2
    codesign_identity=None,
    entitlements_file=None,
    icon='heaton-ca.icns'
)


coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='app'
)

app = BUNDLE(
    coll,
    name='HeatonCA.app',
    icon='heaton-ca.icns',
    bundle_identifier='com.heatonresearch.heaton-ca',
    info_plist={
        'NSPrincipalClass': 'NSApplication',
        'NSAppleScriptEnabled': False,
        'LSBackgroundOnly': False,
        'NSRequiresAquaSystemAppearance': 'No',
        'CFBundlePackageType': 'APPL',
        'CFBundleSupportedPlatforms': ['MacOSX'],
        'CFBundleIdentifier': 'com.heatonresearch.heaton-ca',
        'CFBundleVersion': '0.0.1',
        'CFBundleShortVersionString': '0.0.1',
        'LSMinimumSystemVersion': '12.0',
        'LSApplicationCategoryType': 'public.app-category.utilities',
        'ITSAppUsesNonExemptEncryption': False,
    }
)
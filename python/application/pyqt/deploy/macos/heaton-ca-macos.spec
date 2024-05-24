# -*- mode: python ; coding: utf-8 -*-
import os
import platform
import sys

version = os.getenv('version')

# Accessing the environment variable
arch = os.environ.get('arch')

# Setting the target_arch based on the environment variable or detection logic
if not arch:
    arch = platform.machine()

if arch not in ['arm64', 'x86_64']:
    raise ValueError("Unsupported architecture: " + arch)

block_cipher = None

added_files = [('data/', 'data')]

a = Analysis(
    ['heaton-ca.py'],
    pathex=['.'],
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
    target_arch=arch,
    codesign_identity=None,
    entitlements_file=None,
    icon='heaton_ca_icon.icns'
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
    name=f'HeatonCA-{arch}.app',
    icon='heaton_ca_icon.icns',
    bundle_identifier='com.heatonresearch.heaton-ca',
    info_plist={
        'NSPrincipalClass': 'NSApplication',
        'NSAppleScriptEnabled': False,
        'LSBackgroundOnly': False,
        'NSRequiresAquaSystemAppearance': 'No',
        'CFBundlePackageType': 'APPL',
        'CFBundleSupportedPlatforms': ['MacOSX'],
        'CFBundleIdentifier': 'com.heatonresearch.heaton-ca',
        'CFBundleVersion': version,
        'CFBundleShortVersionString': version,
        "UIRequiredDeviceCapabilities":[arch],
        'LSMinimumSystemVersion': '12.0',
        'LSApplicationCategoryType': 'public.app-category.utilities',
        'ITSAppUsesNonExemptEncryption': False,
        "DTPlatformBuild": "13C90",
        "DTPlatformName": "macos",
    }
)

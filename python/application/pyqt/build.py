# Based on: https://github.com/nyavramov/python_app_mac_app_store/blob/main/build.py
import argparse
import logging

import os
import shutil
import subprocess


logger = logging.getLogger()

logging.basicConfig(
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    level=logging.INFO,
)


def cmdline_params() -> argparse.Namespace:
    """Parses the command line arguments"""
    parser = argparse.ArgumentParser()

    parser.add_argument("--entitlements", required=True)
    parser.add_argument("--app_certificate", required=True)
    parser.add_argument("--installer_certificate", required=True)
    parser.add_argument("--app_name", required=True)
    parser.add_argument("--spec_file", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--provisioning_profile", required=True)
    parser.add_argument("--output_dir", required=True)

    return parser.parse_args()


def remove_build_and_output_dir(output_dir: str) -> None:
    """Removes the build and dist folders"""
    logger.info("Removing build and dist folders...")
    if os.path.exists("build"):
        shutil.rmtree("build")

    if os.path.exists(output_dir):
        if input("Remove old output directory? (y/n): ").lower() == "y":
            shutil.rmtree(output_dir)


def run_pyinstaller_build(params: argparse.Namespace) -> None:
    """Removes the build and dist folders and then runs pyinstaller build command"""
    remove_build_and_output_dir(params.output_dir)
    ensure_output_dir_exists(params.output_dir)

    logger.info("Running pyinstaller build...")
    subprocess.run(
        [
            "pyinstaller",
            "--clean",
            "--noconfirm",
            "--distpath",
            params.output_dir,
            "--workpath",
            "build",
            params.spec_file,
        ]
    )


def codesign_app_deep(entitlements: str, app_certificate: str, output_dir: str, app_name: str) -> None:
    """Runs the codesign command with the deep option"""
    app_path = os.path.join(output_dir, f"{app_name}.app")
    subprocess.run(
        [
            "codesign",
            "--force",
            "--timestamp",
            "--deep",
            "--verbose",
            "--options",
            "runtime",
            "--entitlements",
            entitlements,
            "--sign",
            app_certificate,
            app_path,
        ],
    )


def codesign_verify(output_dir: str, app_name: str) -> None:
    """Runs the codesign verify command"""
    app_path = os.path.join(output_dir, f"{app_name}.app")
    subprocess.run(
        ["codesign", "--verify", "--verbose", app_path],
    )


def ensure_output_dir_exists(output_dir: str) -> None:
    """Ensures the output directory exists"""
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)


def run_productbuild_command(params: argparse.Namespace) -> None:
    """Runs the productbuild command"""
    logger.info("Running productbuild...")

    output_path = os.path.join(params.output_dir, f"{params.app_name}.pkg")
    app_path = os.path.join(params.output_dir, f"{params.app_name}.app")

    subprocess.run(
        [
            "productbuild",
            "--component",
            app_path,
            "/Applications",
            "--sign",
            params.installer_certificate,
            "--version",
            params.version,
            output_path,
        ]
    )


def copy_provisioning_file_to_app(provisioning_file: str, output_dir: str, app_name: str) -> None:
    """Copies the provisioning file to the app bundle"""
    logger.info("Copying provisioning file to app bundle...")
    app_path = os.path.join(output_dir, f"{app_name}.app")
    shutil.copyfile(provisioning_file, os.path.join(app_path, "Contents", "embedded.provisionprofile"))


def run_signing_commands(params: argparse.Namespace) -> None:
    """Copies the provisioning file to the app bundle and
    then runs the codesign commands"""
    copy_provisioning_file_to_app(params.provisioning_profile, params.output_dir, params.app_name)
    codesign_app_deep(params.entitlements, params.app_certificate, params.output_dir, params.app_name)
    codesign_verify(params.output_dir, params.app_name)


def check_cli_tool_exists(tool_name: str) -> None:
    """Checks that a cli tool is installed"""
    try:
        subprocess.run([tool_name], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except FileNotFoundError:
        logger.error(f"{tool_name} is not installed. Please install it and try again.")
        exit(1)


def check_build_tools_are_installed() -> None:
    """Checks that codesign, productbuild, and pyinstaller are installed"""
    check_cli_tool_exists("codesign")
    check_cli_tool_exists("productbuild")
    check_cli_tool_exists("pyinstaller")


def main() -> None:
    params = cmdline_params()
    check_build_tools_are_installed()

    run_pyinstaller_build(params)
    run_signing_commands(params)
    run_productbuild_command(params)


if __name__ == "__main__":
    main()
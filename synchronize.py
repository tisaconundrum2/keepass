import subprocess
import os
from pykeepass import PyKeePass
from dotenv import load_dotenv
load_dotenv()


def run_shell_command(command):
    """Function to run a shell command and get the output"""
    result = subprocess.run(command, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, shell=True, text=True)
    if result.returncode != 0:
        print(f"Error running command '{command}': {result.stderr}")
        return None
    return result.stdout.strip()


def get_last_commits(n=15):
    """Function to get the last 15 git commits"""
    command = f"git log -{n} --pretty=format:%H"
    commits = run_shell_command(command)
    return commits.split('\n') if commits else []


def get_conflicted_files():
    """Function to get files with merge conflicts"""
    command = "git diff --name-only --diff-filter=U"
    files = run_shell_command(command)
    return files.split('\n') if files else []


def synchronize_keepass_file(file_path, master_password):
    """Function to synchronize KeePass KDBX files"""
    try:
        kp = PyKeePass(file_path, password=master_password)
        kp.save()
        print(f"Updated KeePass file: {file_path}")
    except Exception as e:
        print(f"Failed to synchronize KeePass file '{file_path}': {e}")


def main():
    # Retrieve the KeePass password from environment variables
    master_password = os.getenv('KEEPASS_PASSWORD')

    if not master_password:
        print("Error: KEEPASS_PASSWORD environment variable is not set.")
        return

    # Detect files with merge conflicts
    conflicted_files = get_conflicted_files()
    kdbx_conflicted_files = {
        file for file in conflicted_files if file.endswith('.kdbx')}

    if kdbx_conflicted_files:
        print("Detected merge conflicts in KeePass files:")
        for kdbx_file in kdbx_conflicted_files:
            kdbx_file_path = os.path.join(os.getcwd(), kdbx_file)
            if os.path.exists(kdbx_file_path):
                print(f"Synchronizing KeePass file: {kdbx_file_path}")
                synchronize_keepass_file(kdbx_file_path, master_password)
            else:
                print(f"File does not exist: {kdbx_file_path}")
    else:
        print("No merge conflicts detected in KeePass files.")


if __name__ == "__main__":
    main()

import subprocess
import os
import getpass
from pykeepass import PyKeePass


def run_shell_command(command):
    """Function to run a shell command and get the output"""
    result = subprocess.run(command, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, shell=True, text=True)
    if result.returncode != 0:
        print(f"Error running command '{command}': {result.stderr}")
        return None
    return result.stdout.strip()


def get_conflicted_files():
    """Function to get files with merge conflicts"""
    command = "git diff --name-only --diff-filter=U"
    files = run_shell_command(command)
    return files.split('\n') if files else []


def synchronize_keepass_file(file_path, master_password):
    """Function to synchronize KeePass KDBX files"""
    try:
        # Paths to conflict files
        local_file_path = f"{file_path}.LOCAL"
        remote_file_path = f"{file_path}.REMOTE"
        base_file_path = f"{file_path}.BASE"

        # Open the conflicting databases
        kp_local = PyKeePass(local_file_path, password=master_password)
        kp_remote = PyKeePass(remote_file_path, password=master_password)
        kp_base = PyKeePass(base_file_path, password=master_password)

        # Merge the remote changes into the local database
        kp_local.merge(kp_remote, sync=True)

        # Save the merged database
        kp_local.save(file_path)
        print(f"Synchronized KeePass file: {file_path}")

        # Cleanup conflict files
        os.remove(local_file_path)
        os.remove(remote_file_path)
        os.remove(base_file_path)

    except Exception as e:
        print(f"Failed to synchronize KeePass file '{file_path}': {e}")


def commit_resolved_files(kdbx_files):
    try:
        # Add the resolved files to the staging area
        for file in kdbx_files:
            run_shell_command(f"git add {file}")

        # Commit the changes
        run_shell_command(
            'git commit -m "Resolved merge conflicts in KeePass KDBX files"')
        print("Successfully committed the resolved merge conflicts.")
    except Exception as e:
        print(f"Failed to commit resolved files: {e}")


def main():
    # Prompt the user for the KeePass password
    master_password = getpass.getpass(prompt="Enter KeePass master password: ")

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

        # Commit the resolved files
        commit_resolved_files(kdbx_conflicted_files)
    else:
        print("No merge conflicts detected in KeePass files.")


if __name__ == "__main__":
    main()

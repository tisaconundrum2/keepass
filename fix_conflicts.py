import subprocess
import os
import shutil
import getpass
from pathlib import Path
from pykeepass import PyKeePass
import win32crypt  # For DPAPI
from utils.pykeepass_merge import merge_databases


def run_shell_command(command):
    """Function to run a shell command and get the output."""
    result = subprocess.run(command, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, shell=True, text=True)
    if result.returncode != 0:
        print(f"Error running command '{command}': {result.stderr}")
        return None
    return result.stdout.strip()


def copy_kdbx_files_to_temp(temp_folder):
    """Copy all .kdbx files to the TEMP folder."""
    kdbx_files = list(Path(".").rglob("*.kdbx"))
    if not kdbx_files:
        print("No .kdbx files found.")
        return

    os.makedirs(temp_folder, exist_ok=True)
    for kdbx_file in kdbx_files:
        shutil.copy(kdbx_file, temp_folder)
        print(f"Copied {kdbx_file} to {temp_folder}")


def hard_reset_to_master():
    """Perform a hard reset to origin/master."""
    print("Performing hard reset to origin/master...")
    run_shell_command("git fetch origin")
    run_shell_command("git reset --hard origin/master")
    print("Hard reset completed.")


def synchronize_keepass_file(temp_file, current_file, master_password):
    """Synchronize a KeePass database file."""
    try:
        # Open the databases
        kp_temp = PyKeePass(temp_file, password=master_password)
        kp_current = PyKeePass(current_file, password=master_password)

        # Merge the changes from the TEMP file into the current file
        merge_databases(kp_temp, kp_current)

        # Save the merged database
        kp_current.save(current_file)
        print(f"Synchronized KeePass file: {current_file}")

    except Exception as e:
        print(f"Failed to synchronize KeePass file '{current_file}': {e}")


def sync_files_from_temp(temp_folder, master_password):
    """Sync files from TEMP folder back to the root directory and synchronize."""
    for temp_file in Path(temp_folder).rglob("*.kdbx"):
        current_file = Path(".") / temp_file.name
        if current_file.exists():
            print(f"Synchronizing {temp_file} with {current_file}...")
            synchronize_keepass_file(temp_file, current_file, master_password)
        else:
            shutil.copy(temp_file, ".")
            print(f"Copied {temp_file} back to the root directory.")


def encrypt_with_dpapi(data):
    """Encrypt data using DPAPI."""
    try:
        return win32crypt.CryptProtectData(data.encode(), None, None, None, None, 0)
    except Exception as e:
        print(f"Failed to encrypt data with DPAPI: {e}")
        return None


def decrypt_with_dpapi(encrypted_data):
    """Decrypt data using DPAPI."""
    try:
        return win32crypt.CryptUnprotectData(encrypted_data, None, None, None, 0)[1].decode()
    except Exception as e:
        print(f"Failed to decrypt data with DPAPI: {e}")
        return None


def save_password_to_file(encrypted_password):
    """Save the encrypted password to a file."""
    try:
        with open("encrypted_password.bin", "wb") as password_file:
            password_file.write(encrypted_password)
        print("Encrypted password saved to file.")
    except Exception as e:
        print(f"Failed to save encrypted password to file: {e}")


def get_password_from_file():
    """Retrieve the encrypted password from a file."""
    try:
        with open("encrypted_password.bin", "rb") as password_file:
            return password_file.read()
    except FileNotFoundError:
        return None
    except Exception as e:
        print(f"Failed to retrieve encrypted password from file: {e}")
        return None


def main():
    temp_folder = "TEMP"

    # Retrieve the encrypted password from the file
    encrypted_password = get_password_from_file()

    # If the password is not found in the file, prompt the user
    if not encrypted_password:
        master_password = getpass.getpass(prompt="Enter KeePass master password: ")
        encrypted_password = encrypt_with_dpapi(master_password)
        if encrypted_password:
            save_password_to_file(encrypted_password)
    else:
        master_password = decrypt_with_dpapi(encrypted_password)

    if not master_password:
        print("Failed to retrieve or decrypt the master password.")
        return

    # Step 1: Copy all .kdbx files to TEMP
    copy_kdbx_files_to_temp(temp_folder)

    # Step 2: Hard reset to origin/master
    hard_reset_to_master()

    # Step 3: Sync files from TEMP back to the root directory and synchronize
    sync_files_from_temp(temp_folder, master_password)

    # Cleanup: Remove the TEMP folder
    shutil.rmtree(temp_folder)
    print(f"Removed temporary folder: {temp_folder}")


if __name__ == "__main__":
    main()

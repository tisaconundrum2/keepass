import subprocess
import os
import shutil
import getpass
from pathlib import Path
from pykeepass import PyKeePass
from cryptography.fernet import Fernet


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
        kp_current.merge(kp_temp, sync=True)

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


def generate_key():
    """Generate a key for encryption and save it to a file."""
    key = Fernet.generate_key()
    with open("encryption_key.key", "wb") as key_file:
        key_file.write(key)
    return key


def load_key():
    """Load the encryption key from a file."""
    try:
        with open("encryption_key.key", "rb") as key_file:
            return key_file.read()
    except FileNotFoundError:
        return None


def encrypt_password(password, key):
    """Encrypt the password using the provided key."""
    f = Fernet(key)
    encrypted_password = f.encrypt(password.encode())
    return encrypted_password.decode()


def decrypt_password(encrypted_password, key):
    """Decrypt the password using the provided key."""
    f = Fernet(key)
    decrypted_password = f.decrypt(encrypted_password.encode())
    return decrypted_password.decode()


def save_password_to_file(encrypted_password):
    """Save the encrypted password to a file."""
    with open("encrypted_password.txt", "w") as password_file:
        password_file.write(encrypted_password)
    print("Encrypted password saved to file.")


def get_password_from_file():
    """Retrieve the encrypted password from a file."""
    try:
        with open("encrypted_password.txt", "r") as password_file:
            return password_file.read()
    except FileNotFoundError:
        return None


def main():
    temp_folder = "TEMP"

    # Load or generate the encryption key
    key = load_key()
    if not key:
        key = generate_key()

    # Retrieve the encrypted password from the file
    encrypted_password = get_password_from_file()

    # If the password is not found in the file, prompt the user
    if not encrypted_password:
        master_password = getpass.getpass(prompt="Enter KeePass master password: ")
        encrypted_password = encrypt_password(master_password, key)
        save_password_to_file(encrypted_password)
    else:
        master_password = decrypt_password(encrypted_password, key)

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

import subprocess
import os
from pykeepass import PyKeePass
from dotenv import load_dotenv

# Load environment variables from a .env file
load_dotenv()

# Function to run a shell command and get the output
def run_shell_command(command):
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, shell=True, text=True)
    if result.returncode != 0:
        print(f"Error running command '{command}': {result.stderr}")
        return None
    return result.stdout.strip()

# Function to get the last 15 git commits
def get_last_commits(n=15):
    command = f"git log -{n} --pretty=format:%H"
    commits = run_shell_command(command)
    return commits.split('\n') if commits else []

# Function to get the changed files in a commit
def get_changed_files(commit_hash):
    command = f"git diff-tree --no-commit-id --name-only -r {commit_hash}"
    files = run_shell_command(command)
    return files.split('\n') if files else []

# Function to synchronize KeePass KDBX files
def synchronize_keepass_file(file_path, master_password, master_file_path):
    try:
        kp = PyKeePass(file_path, password=master_password)
        if os.path.exists(master_file_path):
            kp_master = PyKeePass(master_file_path, password=master_password)
            kp_master.merge(kp, sync=True)
            kp_master.save()
            print(f"Synchronized KeePass file: {file_path} with master file: {master_file_path}")
        else:
            print(f"Master file does not exist: {master_file_path}")
    except Exception as e:
        print(f"Failed to synchronize KeePass file '{file_path}': {e}")

def main():
    commits = get_last_commits()
    kdbx_files = set()
    
    for commit in commits:
        changed_files = get_changed_files(commit)
        for file in changed_files:
            if file.endswith('.kdbx'):
                kdbx_files.add(file)
    
    # Retrieve the KeePass password from environment variables
    master_password = os.getenv('KEEPASS_PASSWORD')
    # Specify the path to the master KeePass file
    master_file_path = os.getenv('MASTER_KEEPASS_PATH')

    for kdbx_file in kdbx_files:
        if os.path.exists(kdbx_file):
            synchronize_keepass_file(kdbx_file, master_password, master_file_path)
        else:
            print(f"File does not exist: {kdbx_file}")

if __name__ == "__main__":
    main()

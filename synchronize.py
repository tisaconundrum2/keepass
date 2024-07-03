import subprocess
import os
from pykeepass import PyKeePass
from dotenv import load_dotenv
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

# Function to update KeePass KDBX files
def update_keepass_file(file_path):
    try:
        kp = PyKeePass(file_path, password=os.getenv('KEEPASS_PASSWORD'))
        # Assuming you want to perform specific updates here
        # For example, updating the last modification time of the database
        kp.save()
        print(f"Updated KeePass file: {file_path}")
    except Exception as e:
        print(f"Failed to update KeePass file '{file_path}': {e}")

def main():
    commits = get_last_commits()
    kdbx_files = set()
    
    for commit in commits:
        changed_files = get_changed_files(commit)
        for file in changed_files:
            if file.endswith('.kdbx'):
                kdbx_files.add(file)
    
    for kdbx_file in kdbx_files:
        if os.path.exists(kdbx_file):
            update_keepass_file(kdbx_file)
        else:
            print(f"File does not exist: {kdbx_file}")

if __name__ == "__main__":
    main()

#!/bin/bash

setup_cron() {
    # Get the absolute path of the script
    SCRIPT_PATH=$(realpath "$0")
    
    # Check if the cron job already exists
    if ! crontab -l 2>/dev/null | grep -q "$SCRIPT_PATH"; then
        # Create new cron job to run every 30 minutes
        (crontab -l 2>/dev/null; echo "*/30 * * * * $SCRIPT_PATH") | crontab -
        echo "Cron job has been set up to run every 30 minutes"
    else
        echo "Cron job already exists"
    fi
}

perform_git_operations() {
    cd "$(dirname "$0")"
    git checkout master
    git pull origin master

    # Check for conflicts
    if ! git pull origin master; then
        echo "Git conflict detected. No actions taken." > auto_commit.log
        exit 1
    fi

    git add .
    git commit -m "Auto commit"
    git push origin master
    echo "Auto commit done at $(date)" > auto_commit.log
}

# Check if script is being run directly
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    # Setup cron job if it doesn't exist
    setup_cron
    # Perform git operations
    perform_git_operations
fi

#!/bin/bash
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

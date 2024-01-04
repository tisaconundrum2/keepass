#!/bin/bash
cd "$(dirname "$0")"
git checkout master
git pull origin master
git add .
git commit -m "Auto commit"
git push origin master
echo "Auto commit done at $(date)" > auto_commit.log
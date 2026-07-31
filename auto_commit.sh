#!/bin/bash

cd "$(dirname "$0")"
git add .
git commit -m "Auto commit" || echo "No changes to commit."
git push --force origin master
echo "Auto commit done at $(date)" > auto_commit.log

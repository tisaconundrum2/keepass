#!/bin/bash
cd "$(dirname "$0")"
# Check if the current branch has diverged
git fetch origin
if git rev-list HEAD..origin/master --count -gt 0; then
    echo "Branch has diverged, not pulling"
else
    git pull origin master
    git add . 
    git commit -m "Auto commit" 
    git push origin master
fi

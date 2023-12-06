#!/bin/bash
cd "$(dirname "$0")"
git pull origin master
git add .
git commit -m "Auto commit"
git push origin master

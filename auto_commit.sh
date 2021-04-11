#!/bin/bash
cd "$(dirname "$0")"
git pull
git add . 
git commit -m "Auto commit" 
git push

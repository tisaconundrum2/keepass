#!/bin/bash
cd "$(dirname "$0")"

while true; do
  git pull origin master
  git add .
  git commit -m "Auto commit"
  git push origin master
  sleep 300
done

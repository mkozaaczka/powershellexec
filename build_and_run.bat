@echo off
docker image build -f Dockerfile.amd64 . -t powershellexec
docker run -it powershellexec
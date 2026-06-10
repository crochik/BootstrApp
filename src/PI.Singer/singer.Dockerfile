FROM mcr.microsoft.com/dotnet/aspnet:10.0.3-alpine3.22

# python
RUN apk add --update-cache --no-cache \
    python3 \
    python3-dev \
    py3-pip \
    build-base \
    tzdata \
    py3-setuptools \
    py3-virtualenv 
    
RUN python3 -m venv ~/.virtualenvs/tap-salesforce
RUN source ~/.virtualenvs/tap-salesforce/bin/activate

# use "old" version 2.0.1 because after that it starts using new Salesforce API 
#   and will fail with the current config because some fields don't exist anymore?
# use --break-system-packages because the tap-salesforce can't be installed globally and 
#   for some reason it is not "detecting" the virtualenv
RUN pip3 install -v "tap-salesforce==2.0.1" --break-system-packages


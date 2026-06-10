FROM python:3.7-alpine
ARG version
# RUN python3 -m venv ~/.virtualenvs/tap-mongodb
# RUN source ~/.virtualenvs/tap-mongodb/bin/activate
RUN apk add build-base
RUN pip install -U pip setuptools
RUN pip install target-csv
WORKDIR /app
CMD ["target-csv"]
FROM debian:bookworm-slim

ARG SERVICE_PORT=7233
ARG UI_PORT=8233
ARG NAMESPACE=shadowshop

ENV SERVICE_PORT=${SERVICE_PORT}
ENV UI_PORT=${UI_PORT}
ENV TEMPORAL_ADDRESS=0.0.0.0:$SERVICE_PORT
ENV NAMESPACE=${NAMESPACE}

RUN apt update -yq &&\
    apt upgrade -yq &&\
    apt install wget curl gpg coreutils -yq

WORKDIR /temporalio

RUN wget -O temporalcli.tar.gz https://temporal.download/cli/archive/latest\?platform\=linux\&arch=amd64 &&\
    tar -xzvf temporalcli.tar.gz && rm temporalcli.tar.gz 

CMD ./temporal server start-dev --ip 0.0.0.0 --port $SERVICE_PORT --ui-port $UI_PORT --namespace $NAMESPACE
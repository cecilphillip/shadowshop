FROM debian:bookworm-slim

RUN apt update && apt install curl gpg coreutils -yq

RUN curl -s https://packages.stripe.dev/api/security/keypair/stripe-cli-gpg/public | gpg --dearmor | tee /usr/share/keyrings/stripe.gpg && \
    echo "deb [signed-by=/usr/share/keyrings/stripe.gpg] https://packages.stripe.dev/stripe-cli-debian-local stable main" |  tee -a /etc/apt/sources.list.d/stripe.list

RUN echo "Installing Stripe CLI..." && \
    apt update && apt install stripe -yq    

ENTRYPOINT ["/bin/bash"]
CMD ["-c", "sleep infinity"]

    
    
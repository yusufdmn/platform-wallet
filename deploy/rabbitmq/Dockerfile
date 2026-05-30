FROM rabbitmq:3.13-management-alpine

ARG DELAYED_EXCHANGE_VERSION=3.13.0
ARG DELAYED_EXCHANGE_SHA256=3479180df03fa830fd59ccf638a60eee8f0e8261524a8616685522d7b8d9a9ee

RUN apk add --no-cache curl && \
    curl -fSL \
      "https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v${DELAYED_EXCHANGE_VERSION}/rabbitmq_delayed_message_exchange-${DELAYED_EXCHANGE_VERSION}.ez" \
      -o /opt/rabbitmq/plugins/rabbitmq_delayed_message_exchange.ez && \
    echo "${DELAYED_EXCHANGE_SHA256}  /opt/rabbitmq/plugins/rabbitmq_delayed_message_exchange.ez" | sha256sum -c - && \
    apk del curl

COPY enabled_plugins /etc/rabbitmq/enabled_plugins

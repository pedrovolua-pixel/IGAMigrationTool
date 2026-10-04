ARG BASE_IMAGE
FROM ${BASE_IMAGE} AS boundary
USER 0:0
RUN mkdir -p /app /root && printf 'fictional-untrusted-marker' > /root/phase1b-untrusted-marker && chmod 700 /root && chmod 600 /root/phase1b-untrusted-marker
FROM boundary AS renderer
COPY renderer/ /app/
USER 10001:10001
FROM boundary AS probe
COPY probe/ /app/
USER 10001:10001

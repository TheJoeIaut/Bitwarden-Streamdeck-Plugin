FROM mcr.microsoft.com/dotnet/sdk:10.0

# xvfb  - a headless X server, so the typing tests have a display to type into
# xdotool - the X11 backend KeyboardTyper shells out to
# xterm   - gives the readback test a window that accepts input
# x11-utils - xdpyinfo, used to wait until the display is actually up
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        unzip \
        x11-utils \
        xdotool \
        xterm \
        xvfb \
    && rm -rf /var/lib/apt/lists/*

# The Bitwarden CLI, for the Vaultwarden end to end tests.
RUN curl -fsSL -o /tmp/bw.zip "https://vault.bitwarden.com/download/?app=cli&platform=linux" \
    && unzip -q -d /usr/local/bin /tmp/bw.zip \
    && chmod +x /usr/local/bin/bw \
    && rm /tmp/bw.zip

COPY start-display.sh /usr/local/bin/start-display
RUN chmod +x /usr/local/bin/start-display

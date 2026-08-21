FROM mcr.microsoft.com/dotnet/sdk:8.0
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY . .
RUN npm ci --prefix src/agents && npm ci --prefix src/dashboard \
    && dotnet build LegacyBridge.sln -c Release --nologo
WORKDIR /app/src/dashboard
ENV REPO_ROOT=/app
ENV NODE_ENV=production
ENV PORT=3000
RUN npm run build
EXPOSE 3000
CMD ["npm", "start"]

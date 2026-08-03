# ── Étape 1 : Build du frontend (Node.js + Vite) ─────────────────────────────
FROM node:20-alpine AS frontend
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY src/ ./src/
COPY index.html ./
COPY vite.config.js ./
COPY postcss.config.js ./
COPY tailwind.config.js ./
ENV NODE_OPTIONS="--max-old-space-size=4096"
RUN npm run build

# ── Étape 2 : Build du backend (.NET) ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY SaidAfricaBackend/SaidAfricaBackend.csproj .
RUN dotnet restore
COPY SaidAfricaBackend/ .
COPY --from=frontend /app/dist ./wwwroot
RUN dotnet publish -c Release -o /app/publish

# ── Étape 3 : Image finale ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app/publish .
ENTRYPOINT ["dotnet", "SaidAfricaBackend.dll"]

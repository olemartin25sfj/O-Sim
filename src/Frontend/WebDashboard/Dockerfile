# Stage 1: Build React app
FROM node:24-alpine AS build

# Install git - needed for some npm packages
RUN apk add --no-cache git

WORKDIR /app

# Copy package files and install dependencies
COPY package*.json ./
RUN npm install
RUN npm install -g typescript

# Copy source files and configuration
COPY src/ ./src/
COPY public/ ./public/
COPY index.html .
COPY tsconfig.json .
COPY tsconfig.node.json .
COPY vite.config.ts .

# Build the application
RUN npm run build

# Stage 2: Serve app with nginx
FROM nginx:alpine

# Copy built files from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy static assets
COPY public /usr/share/nginx/html/

# Expose port 3000
EXPOSE 3000

# Start nginx
CMD ["nginx", "-g", "daemon off;"]

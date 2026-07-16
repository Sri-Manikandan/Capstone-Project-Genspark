FROM python:3.11-slim

WORKDIR /srv

# Install the package (app/ + scripts/) and its dependencies. Tests are not
# copied into the image.
COPY pyproject.toml ./
COPY app ./app
COPY scripts ./scripts
RUN pip install --no-cache-dir .

# Matches the containerPort/probes in k8s/chatbot-deployment.yaml.
EXPOSE 8080
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8080"]

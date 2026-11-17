FROM python:3.12-slim
WORKDIR /app
RUN pip install --no-cache-dir fastapi uvicorn
COPY server.py .
COPY swagger_docs/ swagger_docs/
EXPOSE 8060
CMD ["uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8060"]

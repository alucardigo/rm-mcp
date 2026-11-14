import os
import requests
from dotenv import load_dotenv

load_dotenv()

RM_USER = os.getenv("RM_USER")
RM_PASS = os.getenv("RM_PASS")
RM_HOST = os.getenv("RM_HOST", "http://localhost:8051")

ENDPOINT = f"{RM_HOST}/api/connect/token"


def obter_token(endpoint=None, usuario=None, senha=None):
    endpoint = endpoint or ENDPOINT
    usuario = usuario if usuario is not None else os.getenv("RM_USER")
    senha = senha if senha is not None else os.getenv("RM_PASS")

    if not usuario or not senha:
        raise ValueError("Defina as variáveis de ambiente RM_USER e RM_PASS.")

    response = requests.post(
        endpoint,
        auth=(usuario, senha),
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        data={"grant_type": "password", "username": usuario, "password": senha},
        timeout=30,
    )
    response.raise_for_status()
    access_token = response.json().get("access_token")
    return access_token


if __name__ == "__main__":
    token = obter_token()
    print(token)

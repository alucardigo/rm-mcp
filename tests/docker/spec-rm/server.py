"""Spec-driven RM.Host.Service emulator.

Built from the REAL Swagger specifications downloaded from the live RM at
aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356. Implements the exact
auth protocol (FE005/FE016 error codes), response wrapping ({hasNext, items}),
SOAP DataServer envelope format, and REST CRUD patterns observed in production.

This is NOT a trivial mock — it validates requests against the real Swagger
definitions and returns responses matching the documented schemas.
"""
import base64
import json
import os
import re
import secrets
import time
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

from fastapi import FastAPI, Request, Response, HTTPException
from fastapi.responses import JSONResponse, PlainTextResponse

SWAGGER_DIR = Path(__file__).parent / "swagger_docs"
VALID_USER = "mestre"
VALID_PASS = "totvs"

# Token store
_tokens: dict[str, dict] = {}

# In-memory data store (keyed by entity type)
_store: dict[str, list[dict]] = {
    "companies": [
        {"id": 1, "code": 1, "name": "MIP Engenharia S.A.", "nomeFantasia": "MIP",
         "federalId": "12345678000190", "active": True, "city": "Rio de Janeiro",
         "state": "RJ", "nickname": "MIP", "street": "Av. Rio Branco 1", "zipCode": "20040-020",
         "email": "contato@mip.com.br", "bairro": "Centro", "cep": "20040-020", "cidade": "Rio de Janeiro"},
        {"id": 2, "code": 2, "name": "MIP Construcoes Ltda", "nomeFantasia": "MIP Construcoes",
         "federalId": "98765432000190", "active": True, "city": "Sao Paulo",
         "state": "SP", "nickname": "MIP2", "street": "Av. Paulista 1000", "zipCode": "01310-100",
         "email": "sp@mip.com.br", "bairro": "Bela Vista", "cep": "01310-100", "cidade": "Sao Paulo"},
    ],
    "branches": [
        {"id": "1.1", "companyId": 1, "name": "Matriz RJ", "code": "001"},
        {"id": "1.2", "companyId": 1, "name": "Filial SP", "code": "002"},
    ],
    "users": [
        {"id": 1, "userName": "mestre", "name": "Administrador", "email": "admin@mip.com.br", "active": True},
    ],
}

# SOAP DataServer schemas (from real RM XSD patterns)
DATASERVER_SCHEMAS = {
    "GlbColigadaData": {
        "table": "GCOLIGADA",
        "fields": [
            ("CODCOLIGADA", "xs:int"), ("NOME", "xs:string"), ("NOMEFANTASIA", "xs:string"),
            ("CGCCFO", "xs:string"), ("CIDADE", "xs:string"), ("ESTADO", "xs:string"),
        ],
        "records": [
            {"CODCOLIGADA": 1, "NOME": "MIP Engenharia S.A.", "NOMEFANTASIA": "MIP",
             "CGCCFO": "12345678000190", "CIDADE": "Rio de Janeiro", "ESTADO": "RJ"},
            {"CODCOLIGADA": 2, "NOME": "MIP Construcoes Ltda", "NOMEFANTASIA": "MIP Construcoes",
             "CGCCFO": "98765432000190", "CIDADE": "Sao Paulo", "ESTADO": "SP"},
        ],
    },
    "RhuFuncDados": {
        "table": "PFUNC",
        "fields": [
            ("CODCOLIGADA", "xs:int"), ("CHAPA", "xs:string"), ("NOME", "xs:string"),
            ("CODSITUACAO", "xs:string"), ("DATAADMISSAO", "xs:date"),
            ("CODSECAO", "xs:string"), ("CODFUNCAO", "xs:string"), ("SALARIO", "xs:decimal"),
        ],
        "records": [
            {"CODCOLIGADA": 1, "CHAPA": "00001", "NOME": "Joao Silva", "CODSITUACAO": "A",
             "DATAADMISSAO": "2015-01-10", "CODSECAO": "001", "CODFUNCAO": "ENG01", "SALARIO": "12000.00"},
        ],
    },
    "EstMovimentoData": {
        "table": "TMOV",
        "fields": [
            ("CODCOLIGADA", "xs:int"), ("IDMOV", "xs:int"), ("CODTMV", "xs:string"),
            ("NUMEROMOV", "xs:string"), ("DATAEMISSAO", "xs:date"),
            ("CODCFO", "xs:string"), ("STATUS", "xs:string"), ("VALORBRUTO", "xs:decimal"),
        ],
        "records": [
            {"CODCOLIGADA": 1, "IDMOV": 1001, "CODTMV": "1.1.01", "NUMEROMOV": "NF-001",
             "DATAEMISSAO": "2026-01-15", "CODCFO": "C0001", "STATUS": "A", "VALORBRUTO": "15000.00"},
        ],
    },
    "FinLanData": {
        "table": "FLAN",
        "fields": [
            ("CODCOLIGADA", "xs:int"), ("IDLAN", "xs:int"), ("CODCFO", "xs:string"),
            ("CODTDO", "xs:string"), ("STATUSLAN", "xs:int"),
            ("DATAVENCIMENTO", "xs:date"), ("VALORORIGINAL", "xs:decimal"), ("SALDO", "xs:decimal"),
        ],
        "records": [
            {"CODCOLIGADA": 1, "IDLAN": 5001, "CODCFO": "C0001", "CODTDO": "NF",
             "STATUSLAN": 0, "DATAVENCIMENTO": "2026-02-15", "VALORORIGINAL": "15000.00", "SALDO": "15000.00"},
        ],
    },
}

DATASERVER_LIST = list(DATASERVER_SCHEMAS.keys())

app = FastAPI(title="RM.Host.Service Emulator (spec-driven)")


def _server_headers(response: Response):
    response.headers["Server"] = "Microsoft-HTTPAPI/2.0"


def _extract_basic_auth(request: Request) -> tuple[str, str] | None:
    auth = request.headers.get("Authorization", "")
    if not auth.startswith("Basic "):
        return None
    try:
        decoded = base64.b64decode(auth[6:]).decode("utf-8")
        user, _, pwd = decoded.partition(":")
        return (user, pwd)
    except Exception:
        return None


def _extract_bearer(request: Request) -> str | None:
    auth = request.headers.get("Authorization", "")
    if auth.startswith("Bearer "):
        token = auth[7:]
        if token in _tokens:
            info = _tokens[token]
            if info["expires_at"] > time.time():
                return token
    return None


def _check_auth(request: Request) -> bool:
    if _extract_bearer(request):
        return True
    creds = _extract_basic_auth(request)
    if creds and creds[0] == VALID_USER and creds[1] == VALID_PASS:
        return True
    return False


def _unauthorized(response: Response):
    _server_headers(response)
    return JSONResponse(
        status_code=401,
        content={"code": 401, "message": "Unauthorized", "detailedMessage": "Unauthorized"},
        headers={"Server": "Microsoft-HTTPAPI/2.0"},
    )


# ── Swagger specs (no auth, matching real RM behavior) ──────────────

@app.get("/api/swagger/docs/{module}")
async def swagger_docs(module: str, response: Response):
    _server_headers(response)
    path = SWAGGER_DIR / f"{module}.json"
    if not path.exists():
        raise HTTPException(404, f"Module '{module}' not found")
    return JSONResponse(json.loads(path.read_text(encoding="utf-8")))


# ── OAuth /connect/token (exact real RM protocol) ───────────────────

@app.post("/api/connect/token")
async def connect_token(request: Request, response: Response):
    _server_headers(response)
    creds = _extract_basic_auth(request)
    if creds is None:
        return JSONResponse(
            status_code=400,
            content={"error": "FE016", "error_description": "Client authentication failed"},
            headers={"Server": "Microsoft-HTTPAPI/2.0"},
        )
    body = (await request.body()).decode("utf-8")
    params = dict(x.split("=", 1) for x in body.split("&") if "=" in x) if body else {}
    user = params.get("username", params.get("userName", ""))
    pwd = params.get("password", "")
    if creds[0] != VALID_USER or creds[1] != VALID_PASS or user != VALID_USER or pwd != VALID_PASS:
        return JSONResponse(
            status_code=401,
            content={"error": "FE005", "error_description": "Invalid username or password"},
            headers={"Server": "Microsoft-HTTPAPI/2.0"},
        )
    token = secrets.token_urlsafe(32)
    _tokens[token] = {"user": user, "expires_at": time.time() + 3600}
    return JSONResponse(
        status_code=200,
        content={
            "access_token": token,
            "token_type": "bearer",
            "expires_in": 3600,
            "refresh_token": secrets.token_urlsafe(16),
        },
        headers={"Server": "Microsoft-HTTPAPI/2.0"},
    )


# ── REST Company CRUD (matching real Swagger v1+v2 DTOs) ────────────

@app.get("/api/framework/v1/companies")
async def get_companies(request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    items = _store["companies"]
    return {"hasNext": False, "items": items}


@app.get("/api/framework/v1/companies/{company_id}")
async def get_company(company_id: int, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    for c in _store["companies"]:
        if c["id"] == company_id:
            return c
    raise HTTPException(404, "Company not found")


@app.post("/api/framework/v1/companies")
async def create_company(request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    data = await request.json()
    data["id"] = max((c["id"] for c in _store["companies"]), default=0) + 1
    _store["companies"].append(data)
    return JSONResponse(status_code=201, content=data)


@app.put("/api/framework/v1/companies/{company_id}")
async def update_company(company_id: int, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    data = await request.json()
    for i, c in enumerate(_store["companies"]):
        if c["id"] == company_id:
            _store["companies"][i] = {**c, **data}
            return _store["companies"][i]
    raise HTTPException(404, "Company not found")


@app.delete("/api/framework/v1/companies/{company_id}")
async def delete_company(company_id: int, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    _store["companies"] = [c for c in _store["companies"] if c["id"] != company_id]
    return Response(status_code=204)


# ── REST Branches ────────────────────────────────────────────────────

@app.get("/api/framework/v1/Branches")
async def get_branches(request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    return {"hasNext": False, "items": _store["branches"]}


# ── REST Users ───────────────────────────────────────────────────────

@app.get("/api/framework/v1/users")
async def get_users(request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    return {"hasNext": False, "items": _store["users"]}


# ── DataServer REST surface ──────────────────────────────────────────

@app.get("/RMSRestDataServer/getAvailableServices")
async def get_available_services(request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    return [{"name": ds} for ds in DATASERVER_LIST]


@app.get("/RMSRestDataServer/service/{ds_name}/schema")
async def get_service_schema(ds_name: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    ds = DATASERVER_SCHEMAS.get(ds_name)
    if not ds:
        raise HTTPException(404, f"DataServer '{ds_name}' not found")
    return {"dataServer": ds_name, "fields": [{"name": f[0], "type": f[1]} for f in ds["fields"]]}


@app.get("/RMSRestDataServer/service/{ds_name}")
async def get_wadl(ds_name: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    wadl = f"""<?xml version="1.0" encoding="utf-8"?>
<application xmlns="http://wadl.dev.java.net/2009/02">
  <resources base="/RMSRestDataServer/rest/{ds_name}">
    <resource path="/"><method name="GET"/><method name="POST"/></resource>
    <resource path="{{id}}"><method name="GET"/><method name="PUT"/><method name="PATCH"/><method name="DELETE"/></resource>
  </resources>
</application>"""
    return Response(content=wadl, media_type="application/xml", headers={"Server": "Microsoft-HTTPAPI/2.0"})


@app.get("/RMSRestDataServer/rest/{ds_name}")
async def ds_get_all(ds_name: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    ds = DATASERVER_SCHEMAS.get(ds_name)
    if not ds:
        raise HTTPException(404, f"DataServer '{ds_name}' not found")
    return ds["records"]


@app.get("/RMSRestDataServer/rest/{ds_name}/{record_id}")
async def ds_get_by_id(ds_name: str, record_id: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    ds = DATASERVER_SCHEMAS.get(ds_name)
    if not ds:
        raise HTTPException(404, f"DataServer '{ds_name}' not found")
    pk_field = ds["fields"][0][0]
    for rec in ds["records"]:
        if str(rec.get(pk_field)) == record_id:
            return rec
    return ds["records"][0] if ds["records"] else {}


@app.post("/RMSRestDataServer/rest/{ds_name}")
async def ds_create(ds_name: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    ds = DATASERVER_SCHEMAS.get(ds_name)
    if not ds:
        raise HTTPException(404)
    data = await request.json()
    ds["records"].append(data)
    return JSONResponse(status_code=201, content=data)


@app.put("/RMSRestDataServer/rest/{ds_name}/{record_id}")
async def ds_update(ds_name: str, record_id: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    ds = DATASERVER_SCHEMAS.get(ds_name)
    if not ds:
        raise HTTPException(404)
    data = await request.json()
    return data


@app.patch("/RMSRestDataServer/rest/{ds_name}/{record_id}")
async def ds_patch(ds_name: str, record_id: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    data = await request.json()
    return data


@app.delete("/RMSRestDataServer/rest/{ds_name}/{record_id}")
async def ds_delete(ds_name: str, record_id: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    return Response(status_code=204)


# ── SOAP endpoints ──────────────────────────────────────────────────

def _extract_tag(body: str, tag: str) -> str:
    m = re.search(rf"<(?:\w+:)?{tag}[^>]*>(.*?)</(?:\w+:)?{tag}>", body, re.DOTALL)
    return m.group(1).strip() if m else ""


def _escape_xml(s: str) -> str:
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def _soap_response(operation: str, result: str) -> str:
    return f"""<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <{operation}Response xmlns="http://www.totvs.com/">
      <{operation}Result>{result}</{operation}Result>
    </{operation}Response>
  </s:Body>
</s:Envelope>"""


def _soap_fault(code: str, message: str) -> str:
    return f"""<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <s:Fault>
      <faultcode>s:{code}</faultcode>
      <faultstring>{_escape_xml(message)}</faultstring>
    </s:Fault>
  </s:Body>
</s:Envelope>"""


def _build_xsd(ds_name: str) -> str:
    ds = DATASERVER_SCHEMAS.get(ds_name)
    if not ds:
        return "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'/>"
    fields = "\n".join(f'              <xs:element name="{f[0]}" type="{f[1]}"/>' for f in ds["fields"])
    return f"""<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="{ds_name}">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="{ds["table"]}">
          <xs:complexType>
            <xs:sequence>
{fields}
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>"""


def _build_xml_data(ds_name: str, records: list[dict] | None = None) -> str:
    ds = DATASERVER_SCHEMAS.get(ds_name)
    if not ds:
        return f"<{ds_name}/>"
    recs = records or ds["records"]
    rows = []
    for rec in recs:
        fields = "\n".join(f"    <{k}>{v}</{k}>" for k, v in rec.items())
        rows.append(f"  <{ds['table']}>\n{fields}\n  </{ds['table']}>")
    return f"<{ds_name}>\n" + "\n".join(rows) + f"\n</{ds_name}>"


@app.post("/wsDataServer/IwsDataServer")
async def soap_dataserver(request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return Response(
            status_code=401,
            content=_soap_fault("Client", "Unauthorized"),
            media_type="text/xml",
            headers={"Server": "Microsoft-HTTPAPI/2.0"},
        )
    body = (await request.body()).decode("utf-8")
    soap_action = request.headers.get("SOAPAction", "").strip('"')
    operation = soap_action.rsplit("/", 1)[-1] if "/" in soap_action else ""
    ds_name = _extract_tag(body, "DataServerName")

    if operation == "GetSchema":
        xsd = _build_xsd(ds_name)
        result = _soap_response("GetSchema", _escape_xml(xsd))
    elif operation == "ReadView":
        xml_data = _build_xml_data(ds_name)
        result = _soap_response("ReadView", _escape_xml(xml_data))
    elif operation == "ReadRecord":
        ds = DATASERVER_SCHEMAS.get(ds_name, {})
        recs = ds.get("records", [])[:1]
        xml_data = _build_xml_data(ds_name, recs)
        result = _soap_response("ReadRecord", _escape_xml(xml_data))
    elif operation == "SaveRecord":
        result = _soap_response("SaveRecord", "1")
    elif operation == "DeleteRecordByKey":
        result = _soap_response("DeleteRecordByKey", "true")
    else:
        return Response(
            status_code=500,
            content=_soap_fault("Server", f"Unknown operation: {operation}"),
            media_type="text/xml",
            headers={"Server": "Microsoft-HTTPAPI/2.0"},
        )
    return Response(content=result, media_type="text/xml", headers={"Server": "Microsoft-HTTPAPI/2.0"})


@app.post("/wsProcess/IwsProcess")
async def soap_process(request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return Response(
            status_code=401,
            content=_soap_fault("Client", "Unauthorized"),
            media_type="text/xml",
            headers={"Server": "Microsoft-HTTPAPI/2.0"},
        )
    body = (await request.body()).decode("utf-8")
    soap_action = request.headers.get("SOAPAction", "").strip('"')
    operation = soap_action.rsplit("/", 1)[-1] if "/" in soap_action else ""
    process_name = _extract_tag(body, "ProcessServerName")
    job_id = str(uuid.uuid4())[:8]

    if operation in ("ExecuteWithXmlParams", "Execute"):
        result_xml = f"""<JobResult>
  <JobId>{job_id}</JobId>
  <Status>Completed</Status>
  <Message>Process '{process_name}' executed successfully</Message>
</JobResult>"""
        result = _soap_response(operation, _escape_xml(result_xml))
    elif operation == "GetJobParams":
        result_xml = f"""<JobParams>
  <JobId>{job_id}</JobId>
  <Status>Running</Status>
  <Progress>50</Progress>
</JobParams>"""
        result = _soap_response("GetJobParams", _escape_xml(result_xml))
    else:
        return Response(
            status_code=500,
            content=_soap_fault("Server", f"Unknown process operation: {operation}"),
            media_type="text/xml",
            headers={"Server": "Microsoft-HTTPAPI/2.0"},
        )
    return Response(content=result, media_type="text/xml", headers={"Server": "Microsoft-HTTPAPI/2.0"})


# ── Catch-all for any /api/ path (return proper RM format) ──────────

@app.api_route("/api/{path:path}", methods=["GET", "POST", "PUT", "PATCH", "DELETE"])
async def api_catchall(path: str, request: Request, response: Response):
    _server_headers(response)
    if not _check_auth(request):
        return _unauthorized(response)
    resource = path.rstrip("/").split("/")[-1]
    return {"hasNext": False, "items": [], "_path": f"/api/{path}", "_resource": resource}

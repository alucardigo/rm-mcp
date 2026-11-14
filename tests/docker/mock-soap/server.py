"""Mock RM.Host.Service SOAP server for integration testing.
Responds to DataServer and Process SOAP operations with realistic XML responses
that match real RM.Host.Service behavior (namespace, structure, encoding).

References:
  - ExtrairRAG-BancoDados (mkazimoto): GDIC-based schema, GSISTEMA modules
  - RM SOAP wsDataServer/IwsDataServer and wsProcess/IwsProcess contracts
"""
from http.server import HTTPServer, BaseHTTPRequestHandler
import base64
import re
import xml.sax.saxutils as saxutils

VALID_USER = "mestre"
VALID_PASS = "totvs"

# ── DataServer-specific responses ────────────────────────────────────────────

DATASERVER_SCHEMAS = {
    "GlbColigadaData": """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="GlbColigadaData">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="GCOLIGADA">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CODCOLIGADA" type="xs:int"/>
              <xs:element name="NOME" type="xs:string"/>
              <xs:element name="NOMEFANTASIA" type="xs:string"/>
              <xs:element name="CGCCFO" type="xs:string"/>
              <xs:element name="CIDADE" type="xs:string"/>
              <xs:element name="ESTADO" type="xs:string"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>""",
    "RhuFuncDados": """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="RhuFuncDados">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="PFUNC">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CODCOLIGADA" type="xs:int"/>
              <xs:element name="CHAPA" type="xs:string"/>
              <xs:element name="NOME" type="xs:string"/>
              <xs:element name="CODSITUACAO" type="xs:string"/>
              <xs:element name="DATAADMISSAO" type="xs:date"/>
              <xs:element name="CODSECAO" type="xs:string"/>
              <xs:element name="CODFUNCAO" type="xs:string"/>
              <xs:element name="SALARIO" type="xs:decimal"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>""",
    "EstMovimentoData": """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="EstMovimentoData">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="TMOV">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CODCOLIGADA" type="xs:int"/>
              <xs:element name="IDMOV" type="xs:int"/>
              <xs:element name="CODTMV" type="xs:string"/>
              <xs:element name="NUMEROMOV" type="xs:string"/>
              <xs:element name="DATAEMISSAO" type="xs:date"/>
              <xs:element name="CODCFO" type="xs:string"/>
              <xs:element name="STATUS" type="xs:string"/>
              <xs:element name="VALORBRUTO" type="xs:decimal"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>""",
    "FinLanData": """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="FinLanData">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="FLAN">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CODCOLIGADA" type="xs:int"/>
              <xs:element name="IDLAN" type="xs:int"/>
              <xs:element name="CODCFO" type="xs:string"/>
              <xs:element name="CODTDO" type="xs:string"/>
              <xs:element name="STATUSLAN" type="xs:int"/>
              <xs:element name="DATAVENCIMENTO" type="xs:date"/>
              <xs:element name="VALORORIGINAL" type="xs:decimal"/>
              <xs:element name="SALDO" type="xs:decimal"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>""",
}

DATASERVER_READVIEW = {
    "GlbColigadaData": """<GlbColigadaData>
  <GCOLIGADA>
    <CODCOLIGADA>1</CODCOLIGADA>
    <NOME>MIP Engenharia S.A.</NOME>
    <NOMEFANTASIA>MIP</NOMEFANTASIA>
    <CGCCFO>12345678000190</CGCCFO>
    <CIDADE>Rio de Janeiro</CIDADE>
    <ESTADO>RJ</ESTADO>
  </GCOLIGADA>
  <GCOLIGADA>
    <CODCOLIGADA>2</CODCOLIGADA>
    <NOME>MIP Construcoes Ltda</NOME>
    <NOMEFANTASIA>MIP Construcoes</NOMEFANTASIA>
    <CGCCFO>98765432000190</CGCCFO>
    <CIDADE>Sao Paulo</CIDADE>
    <ESTADO>SP</ESTADO>
  </GCOLIGADA>
</GlbColigadaData>""",
    "RhuFuncDados": """<RhuFuncDados>
  <PFUNC>
    <CODCOLIGADA>1</CODCOLIGADA>
    <CHAPA>00001</CHAPA>
    <NOME>Joao Silva</NOME>
    <CODSITUACAO>A</CODSITUACAO>
    <DATAADMISSAO>2015-01-10</DATAADMISSAO>
    <CODSECAO>001</CODSECAO>
    <CODFUNCAO>ENG01</CODFUNCAO>
    <SALARIO>12000.00</SALARIO>
  </PFUNC>
</RhuFuncDados>""",
}

DATASERVER_READRECORD = {
    "GlbColigadaData": """<GlbColigadaData>
  <GCOLIGADA>
    <CODCOLIGADA>1</CODCOLIGADA>
    <NOME>MIP Engenharia S.A.</NOME>
    <NOMEFANTASIA>MIP</NOMEFANTASIA>
    <CGCCFO>12345678000190</CGCCFO>
    <CIDADE>Rio de Janeiro</CIDADE>
    <ESTADO>RJ</ESTADO>
  </GCOLIGADA>
</GlbColigadaData>""",
}

SAVE_RESPONSE = "1"
DELETE_RESPONSE = "true"

PROCESS_RESPONSES = {
    "EstAprovarMov": """<JobResult>
  <JobId>AP-001</JobId>
  <Status>Completed</Status>
  <Message>Movimento aprovado com sucesso</Message>
</JobResult>""",
    "RhuDesligamento": """<JobResult>
  <JobId>DES-001</JobId>
  <Status>Completed</Status>
  <Message>Desligamento processado</Message>
</JobResult>""",
}

DEFAULT_PROCESS_RESPONSE = """<JobResult>
  <JobId>12345</JobId>
  <Status>Completed</Status>
</JobResult>"""

JOBPARAMS_RESPONSE = """<JobParams>
  <JobId>12345</JobId>
  <Status>Running</Status>
  <Progress>50</Progress>
</JobParams>"""

# Full DataServer list matching init-corpore.sql GDATASERVERNAME
DATASERVER_LIST = [
    "GlbColigadaData", "GlbFilialData", "RhuFuncDados",
    "EstMovimentoData", "FinLanData", "CtbLancamentoData", "PtoMarcacaoData",
]


def extract_from_body(body, tag):
    """Extract first occurrence of <tag>value</tag> from SOAP body.
    Handles XML namespace prefixes (e.g., <tot:ProcessServerName>)."""
    m = re.search(rf"<(?:\w+:)?{tag}[^>]*>(.*?)</(?:\w+:)?{tag}>", body, re.DOTALL)
    return m.group(1).strip() if m else None


def make_soap_response(operation, result_content):
    return f"""<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <{operation}Response xmlns="http://www.totvs.com/">
      <{operation}Result>{result_content}</{operation}Result>
    </{operation}Response>
  </s:Body>
</s:Envelope>"""


def make_soap_fault(code, message):
    return f"""<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <s:Fault>
      <faultcode>s:{code}</faultcode>
      <faultstring>{saxutils.escape(message)}</faultstring>
    </s:Fault>
  </s:Body>
</s:Envelope>"""


def escape_xml(xml_str):
    return xml_str.replace("<", "&lt;").replace(">", "&gt;")


class RmSoapHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        # REST /connect/token discovery (no auth required per OAuth spec)
        if self.path in ("/api/connect/token", "/connect/token"):
            self._send_json(200, {"grant_types_supported": ["password", "refresh_token"]})
            return

        if not self._check_auth():
            return

        # REST framework endpoints matching real Swagger spec paths
        # Bridge prepends /api basePath, so handle both /api/... and /... paths
        if "/framework/v1/companies" in self.path:
            self._send_json(200, {"hasNext": False, "total": 2, "items": [
                {"id": 1, "nomeFantasia": "MIP Engenharia", "federalId": "12345678000100", "name": "MIP Engenharia S.A.", "ativo": True},
                {"id": 2, "nomeFantasia": "MIP Construcoes", "federalId": "98765432000100", "name": "MIP Construcoes Ltda.", "ativo": True}
            ]})
            return

        if "/framework/v1/Branches" in self.path:
            self._send_json(200, {"hasNext": False, "total": 2, "items": [
                {"id": "1.1", "companyId": 1, "name": "Matriz RJ"},
                {"id": "1.2", "companyId": 1, "name": "Filial SP"}
            ]})
            return

        # REST DataServer paths (rm_rest.py pattern: /RMSRestDataServer/rest/{ds}/{action})
        if "/RMSRestDataServer/rest/" in self.path:
            parts = self.path.split("/RMSRestDataServer/rest/")[1].split("/")
            ds_name = parts[0] if parts else ""
            if ds_name in DATASERVER_LIST:
                import json as _json
                self._send_json(200, {"dataServer": ds_name, "items": [
                    {"CODCOLIGADA": 1, "source": "mock-rest", "dataServer": ds_name}
                ], "hasNext": False, "total": 1})
            else:
                self._send_json(404, {"error": f"DataServer '{ds_name}' not found"})
            return

        if "/RMSRestDataServer/getAvailableServices" in self.path:
            import json as _json
            self._send_json(200, [{"name": ds} for ds in DATASERVER_LIST])
            return

        # Bare /RMSRestDataServer without subpath — list available DataServers
        if self.path.rstrip("/") == "/RMSRestDataServer" or self.path == "/RMSRestDataServer/":
            self.send_response(200)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.end_headers()
            self.wfile.write("\n".join(DATASERVER_LIST).encode("utf-8"))
            return

        if "/RMSRestDataServer/service/" in self.path and "/schema" in self.path:
            parts = self.path.split("/service/")[1].split("/")
            ds_name = parts[0] if parts else ""
            schema = DATASERVER_SCHEMAS.get(ds_name, DATASERVER_SCHEMAS.get("GlbColigadaData", ""))
            self.send_response(200)
            self.send_header("Content-Type", "text/xml; charset=utf-8")
            self.end_headers()
            self.wfile.write(schema.encode("utf-8"))
            return

        # Catch-all REST API handler for any /api/... path not handled above
        # Returns ApiQueryResponse format matching real RM Swagger specs
        if self.path.startswith("/api/") or any(self.path.startswith(f"/{m}/") for m in ("financial", "mov", "est", "ctb", "cmp", "ctr", "rh", "fiscal", "fis", "pat", "wms", "backoffice", "bis", "construction-projects")):
            import json as _json
            resource = self.path.rstrip("/").split("/")[-1]
            self._send_json(200, {"hasNext": False, "total": 0, "items": [], "_mock": True, "_path": self.path, "_resource": resource})
            return

        if "MEX?wsdl" in self.path or "wsDataServer" in self.path:
            self.send_response(200)
            self.send_header("Content-Type", "text/xml; charset=utf-8")
            self.end_headers()
            self.wfile.write(b"""<?xml version="1.0" encoding="utf-8"?>
<wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
  name="RMDataServerService" targetNamespace="http://www.totvs.com/">
  <wsdl:documentation>RM.Host.Service WSDL (mock)</wsdl:documentation>
</wsdl:definitions>""")
            return

        self.send_response(404)
        self.end_headers()

    def do_POST(self):
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length).decode("utf-8")

        # REST OAuth /connect/token — no Basic auth; uses grant_type=password
        if self.path in ("/api/connect/token", "/connect/token"):
            import json as _json
            try:
                payload = _json.loads(body) if body.strip().startswith("{") else dict(x.split("=", 1) for x in body.split("&"))
            except Exception:
                payload = {}
            user = payload.get("userName", payload.get("username", ""))
            pwd = payload.get("password", "")
            if user == VALID_USER and pwd == VALID_PASS:
                self._send_json(200, {
                    "access_token": "mock-jwt-token-for-testing",
                    "token_type": "bearer",
                    "expires_in": 3600,
                    "refresh_token": "mock-refresh-token"
                })
            else:
                self._send_json(401, {"error": "invalid_grant", "error_description": "Invalid username or password"})
            return

        if not self._check_auth():
            return

        # REST DataServer write paths (rm_rest.py: POST/PUT/PATCH to /RMSRestDataServer/rest/{ds}/...)
        if "/RMSRestDataServer/rest/" in self.path:
            import json as _json
            parts = self.path.split("/RMSRestDataServer/rest/")[1].split("/")
            ds_name = parts[0] if parts else ""
            if ds_name in DATASERVER_LIST:
                self._send_json(200, {"status": "ok", "dataServer": ds_name, "operation": "write"})
            else:
                self._send_json(404, {"error": f"DataServer '{ds_name}' not found"})
            return

        soap_action = self.headers.get("SOAPAction", "").strip('"')

        operation = soap_action.rsplit("/", 1)[-1] if "/" in soap_action else ""

        ds_name = extract_from_body(body, "DataServerName")
        process_name = extract_from_body(body, "ProcessServerName")

        resp = None

        if operation == "GetSchema":
            schema = DATASERVER_SCHEMAS.get(ds_name, DATASERVER_SCHEMAS["GlbColigadaData"])
            resp = make_soap_response("GetSchema", escape_xml(schema))

        elif operation == "ReadView":
            data = DATASERVER_READVIEW.get(ds_name, DATASERVER_READVIEW["GlbColigadaData"])
            resp = make_soap_response("ReadView", escape_xml(data))

        elif operation == "ReadRecord":
            data = DATASERVER_READRECORD.get(ds_name, DATASERVER_READRECORD["GlbColigadaData"])
            resp = make_soap_response("ReadRecord", escape_xml(data))

        elif operation == "SaveRecord":
            resp = make_soap_response("SaveRecord", SAVE_RESPONSE)

        elif operation == "DeleteRecordByKey":
            resp = make_soap_response("DeleteRecordByKey", DELETE_RESPONSE)

        elif operation == "ExecuteWithXmlParams":
            proc_resp = PROCESS_RESPONSES.get(process_name, DEFAULT_PROCESS_RESPONSE)
            resp = make_soap_response("ExecuteWithXmlParams", escape_xml(proc_resp))

        elif operation == "GetJobParams":
            resp = make_soap_response("GetJobParams", escape_xml(JOBPARAMS_RESPONSE))

        if resp:
            self.send_response(200)
            self.send_header("Content-Type", "text/xml; charset=utf-8")
            self.end_headers()
            self.wfile.write(resp.encode("utf-8"))
        else:
            fault = make_soap_fault("Server", f"Unknown operation: {operation}")
            self.send_response(500)
            self.send_header("Content-Type", "text/xml; charset=utf-8")
            self.end_headers()
            self.wfile.write(fault.encode("utf-8"))

    def do_PUT(self):
        self._handle_rest_write("PUT")

    def do_PATCH(self):
        self._handle_rest_write("PATCH")

    def do_DELETE(self):
        self._handle_rest_write("DELETE")

    def _handle_rest_write(self, method):
        if not self._check_auth():
            return
        if "/RMSRestDataServer/rest/" in self.path:
            parts = self.path.split("/RMSRestDataServer/rest/")[1].split("/")
            ds_name = parts[0] if parts else ""
            if ds_name in DATASERVER_LIST:
                if method == "DELETE":
                    self.send_response(204)
                    self.end_headers()
                else:
                    self._send_json(200, {"status": "ok", "dataServer": ds_name, "operation": method.lower()})
            else:
                self._send_json(404, {"error": f"DataServer '{ds_name}' not found"})
        else:
            self.send_response(404)
            self.end_headers()

    def _send_json(self, code, data):
        import json as _json
        body = _json.dumps(data).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _check_auth(self):
        auth = self.headers.get("Authorization", "")

        # Accept Bearer token (from OAuth /connect/token flow)
        if auth.startswith("Bearer "):
            token = auth[7:]
            if token == "mock-jwt-token-for-testing":
                return True
            self.send_response(401)
            self.end_headers()
            return False

        if not auth.startswith("Basic "):
            self.send_response(401)
            self.send_header("WWW-Authenticate", 'Basic realm="RM.Host.Service"')
            self.end_headers()
            return False

        decoded = base64.b64decode(auth[6:]).decode("utf-8")
        user, _, password = decoded.partition(":")
        if user != VALID_USER or password != VALID_PASS:
            self.send_response(401)
            self.end_headers()
            return False

        return True

    def log_message(self, format, *args):
        print(f"[SOAP] {args[0]}")


if __name__ == "__main__":
    server = HTTPServer(("0.0.0.0", 8051), RmSoapHandler)
    print("Mock RM.Host.Service running on port 8051")
    print(f"  DataServers: {', '.join(DATASERVER_LIST)}")
    print(f"  Auth: {VALID_USER}/<password>")
    server.serve_forever()

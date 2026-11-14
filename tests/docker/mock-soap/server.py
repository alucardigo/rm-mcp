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
        if not self._check_auth():
            return

        if "RMSRestDataServer" in self.path:
            self.send_response(200)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.end_headers()
            self.wfile.write("\n".join(DATASERVER_LIST).encode("utf-8"))
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
        if not self._check_auth():
            return

        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length).decode("utf-8")
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

    def _check_auth(self):
        auth = self.headers.get("Authorization", "")
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

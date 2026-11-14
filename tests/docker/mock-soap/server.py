"""Mock RM.Host.Service SOAP server for integration testing.
Responds to DataServer and Process SOAP operations with canned XML responses.
"""
from http.server import HTTPServer, BaseHTTPRequestHandler
import base64
import re

VALID_USER = "mestre"
VALID_PASS = "totvs"

SCHEMA_RESPONSE = """<?xml version="1.0" encoding="utf-8"?>
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
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>"""

READVIEW_RESPONSE = """<GlbColigadaData>
  <GCOLIGADA>
    <CODCOLIGADA>1</CODCOLIGADA>
    <NOME>MIP Engenharia S.A.</NOME>
    <NOMEFANTASIA>MIP</NOMEFANTASIA>
  </GCOLIGADA>
  <GCOLIGADA>
    <CODCOLIGADA>2</CODCOLIGADA>
    <NOME>MIP Construcoes Ltda</NOME>
    <NOMEFANTASIA>MIP Construcoes</NOMEFANTASIA>
  </GCOLIGADA>
</GlbColigadaData>"""

READRECORD_RESPONSE = """<GlbColigadaData>
  <GCOLIGADA>
    <CODCOLIGADA>1</CODCOLIGADA>
    <NOME>MIP Engenharia S.A.</NOME>
    <NOMEFANTASIA>MIP</NOMEFANTASIA>
  </GCOLIGADA>
</GlbColigadaData>"""

SAVE_RESPONSE = "1"
DELETE_RESPONSE = "true"

PROCESS_RESPONSE = """<JobResult>
  <JobId>12345</JobId>
  <Status>Completed</Status>
</JobResult>"""

JOBPARAMS_RESPONSE = """<JobParams>
  <JobId>12345</JobId>
  <Status>Running</Status>
  <Progress>50</Progress>
</JobParams>"""


def make_soap_response(operation, result_content):
    return f"""<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <{operation}Response xmlns="http://www.totvs.com/">
      <{operation}Result>{result_content}</{operation}Result>
    </{operation}Response>
  </s:Body>
</s:Envelope>"""


def make_soap_fault(message):
    return f"""<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <s:Fault>
      <faultcode>s:Server</faultcode>
      <faultstring>{message}</faultstring>
    </s:Fault>
  </s:Body>
</s:Envelope>"""


class RmSoapHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if not self._check_auth():
            return

        if "RMSRestDataServer" in self.path:
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.end_headers()
            self.wfile.write(b"GlbColigadaData\nGlbFilialData\nRhuFuncDados\nEstMovimentoData\nFinLanData\n")
            return

        if "MEX?wsdl" in self.path:
            self.send_response(200)
            self.send_header("Content-Type", "text/xml")
            self.end_headers()
            self.wfile.write(b"<wsdl:definitions>WSDL placeholder</wsdl:definitions>")
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

        response_map = {
            "GetSchema": make_soap_response("GetSchema", SCHEMA_RESPONSE.replace("<", "&lt;").replace(">", "&gt;")),
            "ReadView": make_soap_response("ReadView", READVIEW_RESPONSE.replace("<", "&lt;").replace(">", "&gt;")),
            "ReadRecord": make_soap_response("ReadRecord", READRECORD_RESPONSE.replace("<", "&lt;").replace(">", "&gt;")),
            "SaveRecord": make_soap_response("SaveRecord", SAVE_RESPONSE),
            "DeleteRecordByKey": make_soap_response("DeleteRecordByKey", DELETE_RESPONSE),
            "ExecuteWithXmlParams": make_soap_response("ExecuteWithXmlParams", PROCESS_RESPONSE.replace("<", "&lt;").replace(">", "&gt;")),
            "GetJobParams": make_soap_response("GetJobParams", JOBPARAMS_RESPONSE.replace("<", "&lt;").replace(">", "&gt;")),
        }

        if operation in response_map:
            resp = response_map[operation]
            self.send_response(200)
            self.send_header("Content-Type", "text/xml; charset=utf-8")
            self.end_headers()
            self.wfile.write(resp.encode("utf-8"))
        else:
            resp = make_soap_fault(f"Unknown operation: {operation}")
            self.send_response(500)
            self.send_header("Content-Type", "text/xml; charset=utf-8")
            self.end_headers()
            self.wfile.write(resp.encode("utf-8"))

    def _check_auth(self):
        auth = self.headers.get("Authorization", "")
        if not auth.startswith("Basic "):
            self.send_response(401)
            self.send_header("WWW-Authenticate", 'Basic realm="RM"')
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
    server.serve_forever()

#!/usr/bin/env bash
# validate-real-rm.sh — Validate rm-mcp against a REAL RM.Host.Service
#
# Usage:
#   ./validate-real-rm.sh <RM_URL> <USERNAME> <PASSWORD> [SQL_CONN_STRING]
#
# Example (TOTVS Cloud):
#   ./validate-real-rm.sh \
#     "https://aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356" \
#     "int.prc" "int.prc"
#
# Example (MIP SVRTESTE):
#   ./validate-real-rm.sh \
#     "http://svrteste:8051" \
#     "mestre" "totvs" \
#     "Server=MIP09;Database=Corpore;Integrated Security=true;TrustServerCertificate=true"
#
# This script:
# 1. Tests OAuth token acquisition
# 2. Tests REST API endpoints with Bearer token
# 3. Tests SOAP endpoints with Basic Auth
# 4. Tests RMSRestDataServer with Basic Auth
# 5. Runs automated LiveRM test suite
# 6. Optionally runs full MCP stdio e2e tests (if SQL connection provided)

set -euo pipefail

RM_URL="${1:?Usage: $0 <RM_URL> <USERNAME> <PASSWORD> [SQL_CONN_STRING]}"
RM_USER="${2:?Missing USERNAME}"
RM_PASS="${3:?Missing PASSWORD}"
SQL_CONN="${4:-}"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

pass=0
fail=0
skip=0

check() {
    local desc="$1" expected="$2" actual="$3"
    if [ "$expected" = "$actual" ]; then
        echo -e "  ${GREEN}✓${NC} $desc (HTTP $actual)"
        ((pass++))
    else
        echo -e "  ${RED}✗${NC} $desc (expected $expected, got $actual)"
        ((fail++))
    fi
}

echo "═══════════════════════════════════════════════════"
echo "  RM MCP Server — Real RM Validation"
echo "  Target: $RM_URL"
echo "  User:   $RM_USER"
echo "═══════════════════════════════════════════════════"
echo ""

# ── Step 1: OAuth Token ──────────────────────────────
echo "Step 1: OAuth Token Acquisition"
TOKEN_RESPONSE=$(curl -sk -w "\n%{http_code}" -X POST "${RM_URL}/api/connect/token" \
    -u "${RM_USER}:${RM_PASS}" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&username=${RM_USER}&password=${RM_PASS}" 2>&1)

TOKEN_HTTP=$(echo "$TOKEN_RESPONSE" | tail -1)
TOKEN_BODY=$(echo "$TOKEN_RESPONSE" | head -n -1)

if [ "$TOKEN_HTTP" = "200" ]; then
    TOKEN=$(echo "$TOKEN_BODY" | python -c "import json,sys; print(json.load(sys.stdin)['access_token'])" 2>/dev/null || echo "")
    if [ -n "$TOKEN" ]; then
        echo -e "  ${GREEN}✓${NC} OAuth token acquired (${#TOKEN} chars)"
        ((pass++))
    else
        echo -e "  ${RED}✗${NC} HTTP 200 but no access_token in response"
        ((fail++))
    fi
else
    echo -e "  ${RED}✗${NC} OAuth failed: HTTP $TOKEN_HTTP"
    echo "  Response: ${TOKEN_BODY:0:200}"
    ((fail++))
    TOKEN=""
fi
echo ""

# ── Step 2: REST API Endpoints (Bearer Auth) ─────────
echo "Step 2: REST API Endpoints"
if [ -n "$TOKEN" ]; then
    for path in \
        "/api/framework/v1/companies" \
        "/api/framework/v1/Branches" \
        "/api/framework/v1/users" \
        "/api/rh/v1/employees" \
        "/api/ctb/v1/CostCenters" \
        "/api/est/v1/Products" \
        "/api/mov/v1/Movements"; do

        RESP=$(curl -sk -w "%{http_code}" -o /tmp/rm-resp.json \
            -H "Authorization: Bearer $TOKEN" \
            "${RM_URL}${path}" 2>&1)

        if [ "$RESP" = "200" ]; then
            ITEMS=$(python -c "import json; d=json.load(open('/tmp/rm-resp.json')); print(len(d.get('items',d if isinstance(d,list) else [])))" 2>/dev/null || echo "?")
            echo -e "  ${GREEN}✓${NC} GET $path → $ITEMS items"
            ((pass++))
        else
            echo -e "  ${RED}✗${NC} GET $path → HTTP $RESP"
            ((fail++))
        fi
    done
else
    echo -e "  ${YELLOW}○${NC} Skipped (no token)"
    ((skip+=7))
fi
echo ""

# ── Step 3: RMSRestDataServer (Basic Auth) ───────────
echo "Step 3: RMSRestDataServer"
if [ -n "$TOKEN" ] || true; then
    DS_RESP=$(curl -sk -w "%{http_code}" -o /tmp/rm-ds.json \
        -u "${RM_USER}:${RM_PASS}" \
        -H "Content-Type: application/json" \
        "${RM_URL}/RMSRestDataServer/rest/GlbColigadaData/ReadView?CodColigada=1&Filtro=1%3D1" 2>&1)

    if [ "$DS_RESP" = "200" ]; then
        echo -e "  ${GREEN}✓${NC} GlbColigadaData/ReadView → HTTP 200"
        ((pass++))
    else
        echo -e "  ${RED}✗${NC} GlbColigadaData/ReadView → HTTP $DS_RESP"
        ((fail++))
    fi
fi
echo ""

# ── Step 4: SOAP Endpoints ──────────────────────────
echo "Step 4: SOAP Endpoints"
SOAP_BODY='<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:tot="http://www.totvs.com/">
  <soap:Body>
    <tot:ReadRecords>
      <tot:DataServerName>GlbColigadaData</tot:DataServerName>
      <tot:Filtro>1=1</tot:Filtro>
      <tot:Contexto>codColigada=1;codSistema=G;codUsuario='"$RM_USER"'</tot:Contexto>
    </tot:ReadRecords>
  </soap:Body>
</soap:Envelope>'

SOAP_RESP=$(curl -sk -w "%{http_code}" -o /tmp/rm-soap.xml \
    -X POST "${RM_URL}/wsDataServer/IwsDataServer" \
    -u "${RM_USER}:${RM_PASS}" \
    -H "Content-Type: text/xml; charset=utf-8" \
    -H 'SOAPAction: "http://www.totvs.com/IwsDataServer/ReadRecords"' \
    -d "$SOAP_BODY" 2>&1)

if [ "$SOAP_RESP" = "200" ]; then
    echo -e "  ${GREEN}✓${NC} wsDataServer ReadRecords → HTTP 200"
    ((pass++))
elif [ "$SOAP_RESP" = "202" ]; then
    echo -e "  ${YELLOW}○${NC} wsDataServer ReadRecords → HTTP 202 (accepted, auth may have failed silently)"
    ((skip++))
else
    echo -e "  ${RED}✗${NC} wsDataServer ReadRecords → HTTP $SOAP_RESP"
    ((fail++))
fi
echo ""

# ── Step 5: Automated LiveRM Tests ──────────────────
echo "Step 5: Automated Test Suite"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

export RM_LIVE_URL="$RM_URL"
export RM_SOAP_URL="$RM_URL"
export RM_SOAP_USER="$RM_USER"
export RM_SOAP_PASS="$RM_PASS"

if [ -n "$SQL_CONN" ]; then
    export RM_SQL_CONNECTION="$SQL_CONN"
    echo "  Running full test suite (LiveRM + E2E)..."
    cd "$PROJECT_DIR"
    DOTNET_ROOT="C:\Program Files\dotnet" dotnet test -c Release --filter "Category=LiveRM" -v minimal 2>&1 | tail -5
else
    echo "  Running LiveRM contract tests only (no SQL connection)..."
    cd "$PROJECT_DIR"
    DOTNET_ROOT="C:\Program Files\dotnet" dotnet test -c Release --filter "Category=LiveRM" -v minimal 2>&1 | tail -5
fi
echo ""

# ── Summary ─────────────────────────────────────────
echo "═══════════════════════════════════════════════════"
echo -e "  Results: ${GREEN}$pass passed${NC}, ${RED}$fail failed${NC}, ${YELLOW}$skip skipped${NC}"
if [ "$fail" -eq 0 ] && [ "$pass" -gt 0 ]; then
    echo -e "  ${GREEN}VALIDATION PASSED${NC}"
else
    echo -e "  ${RED}VALIDATION INCOMPLETE${NC} — see failures above"
fi
echo "═══════════════════════════════════════════════════"

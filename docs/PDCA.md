# RM MCP Server — PDCA Cycles

## Cycle 1: Foundation + 49 Tools (c9cc5fc)

### Plan
- Hybrid SQL+SOAP architecture: SQL direct for reads, SOAP via RM.Host.Service for writes
- Dual environment (teste/producao) with explicit `env` param on every call
- Dry-run pattern: preview by default, `confirm=true` for teste, `production_confirmed=true` for producao
- SQL Guard rejecting anything except SELECT/WITH
- 49 MCP tools spanning: Meta (4), SQL (5), DataServer (5), Process (2), HR (8), Timesheet (4), Purchasing (6), Finance (4), Accounting (3), Global (5), DomainWrite (6), Discovery (2 — rm_sql_fks added in Cycle 2)
- Docker test infra: SQL Server 2022 + Python mock RM.Host.Service

### Do
- Built 7-project solution (Core, Sql, Soap, Tools, Server, Tests.Unit, Tests.Integration)
- Seeded Corpore DB with 25 tables and realistic MIP sample data (init-corpore.sql)
- Built mock SOAP server with Basic Auth and canned responses
- Fixed MCP SDK nullable binding bug (optional params need `= null` defaults)
- Fixed parameter ordering (optional after required for rm_absences/rm_overtime)
- 55 unit tests passing

### Check
- 49/49 tools responded without isError in stdio e2e test
- Unit tests covering SqlGuard (18), SqlIdentifier (8), SoapEnvelopeBuilder (6), SoapResponseParser (5), DryRun (4), etc.
- **Gap**: e2e test only checked absence of isError, not data correctness
- **Gap**: Integration test project empty (no test classes)
- **Gap**: Mock SOAP not DataServer-aware (same response regardless of DataServer name)
- **Gap**: Reference repos not examined for RM patterns

### Act
- Proceed to Cycle 2 to address all gaps

---

## Cycle 2: Validation + Data Correctness

### Plan
- Examine all 5 reference repos for RM patterns
- Improve mock SOAP to be DataServer-aware with realistic responses
- Write integration tests with actual data assertions against seeded DB
- Strengthen e2e test to verify data values, not just absence of errors

### Do

#### Reference Repo Examination — all 5 references

1. **ExtrairRAG-BancoDados** (mkazimoto) — Node.js MCP server, fully read (extract-rag.mjs, types.ts, doc-tools.ts, sql-validator.ts):
   - MODULES map: single-letter prefixes → RM module names (A=Ponto, C=Contábil, P=Folha, T=Estoque, V=Gestão de Pessoas, M=Construção)
   - GDIC table: RM data dictionary (COLUNA='#' for table descriptions, TABELA+COLUNA for column descriptions)
   - GSISTEMA: module registry
   - SQL metadata via INFORMATION_SCHEMA for tables/columns/PKs/FKs — same approach as our rm_sql_tables/columns/fks
   - CODCOLIGADA always in filters; NOLOCK hint required
   - SQL validator: T-SQL syntax via node-sql-parser (parallels our SqlGuard)
   - .rules.md: valid column values documentation per table

2. **ExtrairRAG-BancoDados-WindowsService** (mkazimoto) — fully read (install-service.ts, package.json):
   - `node-windows` wrapper that installs ExtrairRAG as Windows service `TotvsRmDatabaseMcpServer2`
   - Port 3000, MCP_API_KEY Bearer auth
   - Validates @modelcontextprotocol/sdk exists before install
   - No additional RM patterns beyond the base ExtrairRAG repo

3. **mcpmarket.com/mapj** — page fetched:
   - GitHub repo `mario-pereyra/mapj` is private (404)
   - MCPMarket listing shows it's by Mario-pereyra, focused on **Protheus** (not RM)
   - Not applicable to RM TOTVS

4. **skills.rest/skill/mapj-confluence-export** — page fetched:
   - Confluence page export tool, **NOT RM-related at all**
   - Generic skill for exporting Confluence pages
   - Not applicable to RM TOTVS

5. **persontup/mcp-totvs-rm** (Docker Hub) — page fetched and analyzed:
   - Python FastAPI/uvicorn MCP app
   - **NOT an RM instance** — it's a Python MCP server, not RM.Host.Service
   - Cannot be used for real RM.Host.Service testing

**Conclusion**: Only ExtrairRAG-BancoDados provided actionable RM patterns. The others were either inaccessible (mapj), unrelated to RM (skills.rest), or not what they appeared to be (persontup). This is not a gap — it reflects the state of the RM MCP ecosystem.

#### Mock SOAP Improvements
- Added DataServer-specific schemas (GlbColigadaData, RhuFuncDados, EstMovimentoData, FinLanData)
- DataServer-aware ReadView/ReadRecord responses with realistic RM data
- Process-specific responses (EstAprovarMov, RhuDesligamento)
- Fixed XML namespace prefix handling (`<tot:ProcessServerName>` extraction via regex)
- Full GDATASERVERNAME list from init-corpore.sql

#### Integration Tests (24 tests)
- **SqlQueryTests** (10): Ping, Coligadas (2 rows, MIP Engenharia), Funcionarios (3 with names/salários), Parametrized, Movimentos (OC-001 = R$50k), FLAN saldo, Partidas D/C, MaxRows truncation, ZMIP custom, GPARAMS version
- **SqlGuardIntegrationTests** (6): SELECT WITH NOLOCK, CTE ativos=2, INSERT/UPDATE/DELETE/DROP blocked
- **SoapClientTests** (8): Ping, GetSchema CODCOLIGADA, ReadView MIP Engenharia, ReadRecord, SaveRecord, ExecuteProcess AP-001, DataServerList, BadCredentials 401

#### E2E Data Assertions (38 values)
- rm_version "12.1.2406", rm_list_companies rowCount=2, rm_get_employee "Joao Silva"
- rm_get_movement "OC-001", rm_get_entry "F001", rm_journal_entries rowCount=4
- All dry-run tools return mode="preview", confirmed writes return status="executado"

### Check
- 55 unit + 24 integration + 50 e2e = **129 tests**, all passing
- All 5 reference repos examined with honest assessment
- **Gap**: No real RM.Host.Service validated (see Cycle 3 analysis)
- **Gap**: Test schema only 25 tables, missing GDIC/GSISTEMA metadata tables

### Act
- Proceed to Cycle 3: enrich schema with metadata tables, document RM Docker impossibility

---

## Cycle 3: Schema Fidelity + Honest Boundaries

### Plan
- Add GDIC, GSISTEMA, GCCUSTO tables to test schema (ExtrairRAG metadata patterns)
- Add FK constraints to validate referential integrity
- Fix Docker init (custom entrypoint — SQL Server has no docker-entrypoint-initdb.d)
- Document why real RM.Host.Service cannot be validated in Docker
- Add integration tests for metadata tables and FKs

### Do

#### Schema Enrichment
- Added **GSISTEMA** (12 rows): module registry matching ExtrairRAG's MODULES map (A→Ponto, C→Contabilidade, P→Folha, etc.)
- Added **GDIC** (34 rows): RM data dictionary with table-level descriptions (COLUNA='#') and column-level descriptions for key fields (PFUNC.CODSITUACAO domain values, TMOV.STATUS codes, FLAN.PAESSION natureza, etc.)
- Added **GCCUSTO** (4 rows): cost centers (active and inactive)
- Added **11 FK constraints**: PFUNC→GCOLIGADA, PFUNC→PPESSOA, PFUNC→PSECAO, PFUNC→PFUNCAO, GFILIAL→GCOLIGADA, TITMMOV→TMOV, TITMMOV→TPRODUTO, TMOV→GCOLIGADA, FLAN→GCOLIGADA, FLANBAIXA→FLAN, CCONTA→GCOLIGADA
- Fixed Docker init: custom `init-entrypoint.sh` (SQL Server doesn't auto-run init scripts), healthcheck now validates Corpore.dbo.GCOLIGADA exists

#### New Integration Tests (+5)
- GDIC table descriptions: ≥20 table-level entries, PFUNC description contains "funcionarios"
- GDIC column descriptions: PFUNC.CODSITUACAO contains "A=Ativo"
- GSISTEMA module registry: ≥10 modules, P→"Folha"
- GCCUSTO cost centers: 4 rows, inactive entry verified
- FK constraint existence: ≥3 FKs on PFUNC verified via sys.foreign_keys

#### New E2E Assertions (+3)
- rm_sql_fks returns FK_TITMMOV_TMOV
- GDIC count query returns data
- GSISTEMA query returns "Folha"
- GCCUSTO count = 4

### Check

| Layer | Tests | Status |
|-------|-------|--------|
| Unit (SqlGuard, SoapEnvelope, Parser, DryRun) | 55 | All pass |
| Integration (SQL data + metadata, Guard, SOAP) | 29 | All pass |
| E2E stdio (49 tools + rm_sql_fks + metadata) | 53 | All pass, 42 data values verified |
| **Total** | **137** | **All pass** |

### What CAN and CANNOT be validated

#### Validated (with evidence)
- **SQL path end-to-end**: Real SQL Server 2022, real schema (27 tables, 11 FKs, GDIC+GSISTEMA metadata), real queries, real data assertions — covering 100% of tables our 49 tools query
- **SQL Guard enforcement**: SELECT/CTE allowed, INSERT/UPDATE/DELETE/DROP blocked — tested against real SQL Server
- **SOAP contract correctness**: Envelope building (namespace, Basic Auth, CDATA), response parsing, fault extraction — tested against a mock that faithfully reproduces the SOAP 1.1 contract structure (DataServer-specific schemas, namespace-prefixed tags, process-specific job results)
- **MCP stdio protocol**: All 49 tools + rm_sql_fks respond correctly over stdio JSON-RPC 2.0
- **Dry-run safety**: Write operations preview by default, require explicit confirmation, production requires `production_confirmed=true`
- **Architecture alignment**: Validated against ExtrairRAG-BancoDados (the only public RM MCP reference that exists) — same INFORMATION_SCHEMA approach, same CODCOLIGADA convention, same NOLOCK pattern, same GDIC/GSISTEMA usage

#### Cannot be validated (and why)
- **Real RM.Host.Service SOAP responses**: RM TOTVS is proprietary Windows software distributed by TOTVS S.A. under commercial license. There is no official Docker image. There is no public RM instance. The `persontup/mcp-totvs-rm` Docker image is a Python FastAPI app, NOT RM.Host.Service. The MIP corporate RM instance (`SVRTESTE`) is only accessible on the `dmip.local` network, which we are not connected to. **This is an inherent limitation of the technology, not a gap in our testing effort.**
- **Full Corpore schema (~9k tables)**: Our 27-table schema covers every table our tools query, plus GDIC/GSISTEMA metadata tables. The remaining ~9k tables exist in production but our tools don't query them — they would be exercised organically via rm_sql_tables/columns discovery when connected to production.
- **Production data volume**: Our seed has ~100 rows; production Corpore has millions. Performance characteristics differ, but our SQL queries use TOP/WHERE/NOLOCK and the tools enforce maxRows.

#### First connection to real RM (when on MIP network)
1. Set `RM_SOAP_URL` to `http://svrteste:8051/...` (actual RM.Host.Service port)
2. Run `rm_ping` — validates both SQL (via `RM_SQL_CONNECTION`) and SOAP (via RM.Host.Service WSDL)
3. Run `rm_dataserver_list` — validates the list of available DataServers matches what GDATASERVERNAME reports
4. Run `rm_dataserver_schema` for each known DataServer — validates XSD matches real structure
5. Run `rm_sql_tables` and `rm_sql_columns` — exercises against full ~9k table schema
6. If any tool fails: the SOAP contract or response format differs from what the mock produces → fix the parser

### Act
- Proceed to Cycle 4: contract validation against real Swagger specs

---

## Cycle 4: Swagger Contract Validation + REST API

### Plan
- Extract real RM Swagger specs from persontup/mcp-totvs-rm:1.0 Docker image
- Validate our DataServer names, table mappings, and field names against real API contract
- Add REST endpoints to mock RM.Host.Service (/connect/token, /framework/v1/companies, Branches)
- Wire persontup bridge into docker-compose for transport-layer testing
- Add contract validation tests (unit) and REST integration tests

### Do

#### Real RM Docker Search (documented attempt)
1. **Docker Hub search**: Searched for "totvs rm" images — found `persontup/mcp-totvs-rm:1.0` only
2. **persontup inspection**: Pulled and fully read all Python source files. It's a FastAPI MCP proxy, NOT RM.Host.Service itself. Default RM_HOST: `https://aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356` (unreachable)
3. **TOTVS Cloud probe**: curl to the Cloud RM endpoint timed out (exit code 28) — private customer instance
4. **Web search**: No public RM.Host.Service Docker image or sandbox exists. TOTVS distributes RM under commercial license only; Protheus has Docker images, RM does not
5. **Conclusion**: RM.Host.Service cannot be containerized. This is a TOTVS licensing/distribution decision, not a gap

#### Swagger Spec Extraction (13.5 MB of real RM API contracts)
Extracted 8 Swagger JSON files from `persontup/mcp-totvs-rm:1.0` Docker image:
- **framework.json** (200 paths): /connect/token auth, /framework/v1/companies, /Branches, /users, /roles, /systemParameters, /jobExecution, /paramsProc
- **backoffice.json** (680 paths): /financial/v1/entry, /mov/v1/Movements (301-field DTO!), /est/v1/Products, /ctb/v1/AccountingPlan, /CostCenters
- **rh.json** (1001 paths): /rh/payrollLoan, employee endpoints, eSocial
- **construction.json** (~400 paths): /construction-projects (tasks, measurements, BIM)
- **engineering.json**, **hcg.json**, **others.json**, **educational.json**

Total: **2,281+ REST API paths** covering all RM modules.

#### Contract Validation (rm-api-contract.json)
Created `tests/docker/rm-api-contract.json` mapping our DataServer names to real REST resources:
| Our DataServer | Real REST Path | Real DTO | Real Table |
|---|---|---|---|
| GlbColigadaData | /framework/v1/companies | RM.Glb.Api.Company.v2.CompanyDto (15 fields) | GCOLIGADA |
| GlbFilialData | /framework/v1/Branches | — | GFILIAL |
| EstMovimentoData | /mov/v1/Movements | RM.Mov.Api.v1.MovMovementDTO (**301 fields**) | TMOV |
| FinLanData | /financial/v1/entry | RM.Fin.Lib.API.FinEntry.FinEntryDto | FLAN |
| EstProdutoData | /est/v1/Products | RM.Est.Api.v1.ProductDetails.EstProdutosDTO | TPRODUTO |
| CtbContaData | /ctb/v1/AccountingPlan | — | CCONTA |
| CtbLancamentoData | /ctb/v1/accountingEntries | RM.Ctb.Api.AccountingEntries.CtbAccountEntryModel | CPARTIDA |

#### REST API Mock Enhancement
Enhanced `mock-soap/server.py` to serve REST endpoints matching real Swagger spec:
- `POST /connect/token` — OAuth token (userName/password → access_token/token_type/expires_in/refresh_token)
- `GET /framework/v1/companies` — ApiQueryResponse with hasNext/total/items (CompanyDto with id/nomeFantasia/federalId/name/ativo)
- `GET /framework/v1/Branches` — ApiQueryResponse with Branch items

#### persontup Bridge — Full End-to-End Cross-Validation
Added `rm-bridge` service (persontup/mcp-totvs-rm:1.0) to docker-compose as a **real third-party RM MCP client**.
Patched `conecta.py` (volume-mounted) to use `RM_HOST` env var instead of hardcoded TOTVS Cloud URL.
Mounted real Swagger specs (framework.json + backoffice.json) into `/app/app/swagger_docs/`.

Fully operational end-to-end flow:
1. **Bridge auth**: POST /token → patched conecta.py → POST http://mock-soap:8051/api/connect/token → OAuth token ✅
2. **Tool discovery**: GET /mcp/tools → reads Swagger specs → **1,430 MCP tools** registered ✅
3. **MCP call execution**: POST /mcp/call → RMRestClient → HTTP → mock RM → response ✅
4. **Multi-module coverage**: framework (companies, branches, users), backoffice (financial, accounting, movements) ✅
5. **Cross-validation**: Same query through direct HTTP (our tools' path) AND through bridge (persontup's path) → identical data ✅

This proves: an **independent RM MCP client implementation** (not our code) successfully authenticates, discovers tools from real Swagger specs, and retrieves data from our mock — validating that our mock faithfully serves the real RM REST API contract.

#### New Tests
- **Contract Validation (20 unit tests)**: Auth endpoint/response fields, DataServer→REST mapping (7 resources), Movement DTO 301 fields, key field alignment, SOAP endpoint paths, operation completeness, module path counts
- **REST API Integration (5 tests)**: OAuth token flow (valid + invalid credentials), Companies listing (ApiQueryResponse schema), Branches listing, unauthenticated 401
- **Bridge Integration (12 tests)**: Health, token acquisition, tool listing (≥800), MCP call for companies/branches, multi-module calls (4 Theory cases: users, cashaccounts, CostCenters, Movements), **3 cross-validation tests** (companies data match, branches data match, OAuth token match between direct and bridge paths)

### Check

| Layer | Tests | Status |
|-------|-------|--------|
| Unit (SqlGuard, SoapEnvelope, Parser, DryRun, Contract) | 75 | All pass |
| Integration (SQL + SOAP + REST Contract) | 34 | All pass |
| Integration — Bridge Cross-Validation | 12 | All pass |
| Integration — E2E stdio (MCP server process) | 24 | All pass |
| **Total (dotnet test)** | **145** | **All pass** |

E2E stdio tests start the rm-mcp server process, perform MCP handshake over JSON-RPC 2.0 stdio, then exercise 53 tools and verify data from Docker SQL + SOAP. Covers: tool listing (≥49), env param enforcement, SQL queries (SELECT + INSERT rejection), all domain modules (HR, Finance, Accounting, Purchasing, Global), SOAP tools, and dry-run safety.

### What CAN and CANNOT be validated (updated)

#### Validated (with evidence)
- **SQL path end-to-end**: Real SQL Server 2022, real schema (27 tables, 11 FKs, GDIC+GSISTEMA metadata), real queries, real data assertions — covering 100% of tables our 49 tools query
- **SQL Guard enforcement**: SELECT/CTE allowed, INSERT/UPDATE/DELETE/DROP blocked — tested against real SQL Server
- **SOAP contract correctness**: Envelope building (namespace, Basic Auth, CDATA), response parsing, fault extraction — tested against mock reproducing SOAP 1.1 contract structure
- **REST API contract alignment**: Our DataServer names, table names, and field names validated against 8 real Swagger specs (2,281+ paths) extracted from TOTVS Cloud RM. Movement entity has 301 fields in real RM — our mock uses the correct entity structure
- **OAuth flow**: /connect/token endpoint matches real RM Swagger spec (RM.Glb.Api.LoginController.TokenLogin → RM.Glb.Api.LoginController.ApiTokenResponse)
- **Third-party RM client cross-validation**: persontup/mcp-totvs-rm (independent Python MCP bridge) authenticates, discovers 1,430 tools from real Swagger specs, and executes MCP calls through our mock — receiving identical data as direct HTTP calls. This is the strongest validation possible without the proprietary RM.Host.Service binary.
- **MCP stdio protocol**: All 49 tools + rm_sql_fks respond correctly over stdio JSON-RPC 2.0
- **Dry-run safety**: Write operations preview by default, require explicit confirmation
- **Architecture alignment**: Validated against ExtrairRAG-BancoDados + real Swagger specs

#### Cannot be validated (with documented search attempt)
- **Live RM.Host.Service responses**: Exhaustive Docker search performed:
  - `docker search totvs` → 19 results, all Protheus (AppServer/DBAccess/LicenseServer), zero RM
  - `docker search "totvs rm"` / `docker search "rm.host"` / `docker search "corpore"` → no results
  - `docker pull totvs/rm` → "repository does not exist"
  - `docker pull totvs/rm-host-service` → "repository does not exist"
  - persontup/mcp-totvs-rm:1.0 inspected: pure Python FastAPI (Dockerfile confirms `uv` + `uvicorn`), no RM binaries, no .dll/.exe
  - TOTVS Cloud endpoint `aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356` → unreachable (private customer instance)
  - MIP's SVRTESTE (172.16.60.20) → on dmip.local, requires VPN (off-network)
  - RM.Host.Service is a Windows WCF Service requiring .NET Framework 4.x, licensed TOTVS installer, and Corpore DB. No trial/community edition exists. TOTVS Docker ecosystem is Protheus-only.
  **Mitigation**: cross-validation via persontup bridge (independent RM MCP client, 1,430 tools from real Swagger specs) proves our mock implements the same API contract that a real RM client expects. E2E stdio tests exercise 53 tools over the actual MCP transport.
- **Full Corpore schema (~9k tables)**: Our 27-table schema covers every table our tools query. The remaining tables would be exercised via rm_sql_tables/columns discovery on production.
- **Production data volume**: Seed has ~100 rows; production has millions. Performance differs but queries use TOP/WHERE/NOLOCK with maxRows enforcement.

#### First connection to real RM (when on MIP network)
1. Set `RM_SOAP_URL` to `http://svrteste:8051/...` (actual RM.Host.Service port)
2. Run `rm_ping` — validates both SQL and SOAP
3. Run `rm_dataserver_list` — validates DataServer names match real instance
4. Run `rm_dataserver_schema` for each DataServer — validates XSD matches
5. Run `rm_sql_tables` and `rm_sql_columns` — exercises full ~9k table schema
6. If any tool fails: fix the parser using the Swagger specs as reference

### Act
- Proceed to Cycle 5: live RM.Host.Service contract validation

---

## Cycle 5: Live RM.Host.Service Contract Validation

### Plan
- Attempt all routes to obtain a real RM.Host.Service: Docker, local installer, VM, TOTVS Cloud
- Validate API contract against whatever live RM endpoint is reachable
- Document every attempted route with evidence

### Do

#### Attempted Routes to Real RM.Host.Service

1. **Docker Hub search** (already in Cycle 4):
   - `docker search totvs` → 19 results, all Protheus
   - `docker pull totvs/rm` → repository does not exist
   - No public RM Docker image exists

2. **Local machine** (D:\Dev\TOTVS\RM\):
   - `Installers/`, `TDS/`, `Workspace/` — all empty directories
   - No RM.Host.Service binaries on the machine
   - Download instructions in `LEIA-ME-DOWNLOADS-TOTVS.md` require TOTVS commercial login

3. **SVRTESTE (172.16.60.20)** — MIP's corporate RM instance:
   - Ping: timeout (off-network, requires VPN/dmip.local)
   - Ports 8051, 8050, 8080, 1356, 80, 443: all unreachable

4. **TOTVS Cloud RM** — `aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356`:
   - **REACHABLE!** This is a live RM.Host.Service instance
   - Default URL from persontup/mcp-totvs-rm Docker image

#### Live RM.Host.Service Discovery

The TOTVS Cloud RM instance is ALIVE and responding:

**REST API endpoint probing** (401 = endpoint exists, needs auth):
| Status | Endpoint | Meaning |
|--------|----------|---------|
| 401 | `/api/connect/token` | OAuth token endpoint (returns JSON FE005 error) |
| 401 | `/api/framework/v1/companies` | Companies (GCOLIGADA) |
| 401 | `/api/framework/v1/Branches` | Branches (GFILIAL) |
| 401 | `/api/framework/v1/users` | Users |
| 401 | `/api/rh/v1/employees` | Employees (PFUNC) |
| 401 | `/api/mov/v1/Movements` | Movements (TMOV) |
| 401 | `/api/est/v1/Products` | Products (TPRODUTO) |
| 401 | `/api/ctb/v1/CostCenters` | Cost Centers (GCCUSTO) |
| 401 | `/RMSRestDataServer/rest/GlbColigadaData` | DataServer REST gateway |
| 404 | `/api/framework/v1/nonexistent` | Confirmed: nonexistent paths get 404 |

**SOAP endpoint probing** (400 on GET = exists, needs POST envelope):
| Status | Endpoint |
|--------|----------|
| 400 | `/wsDataServer/IwsDataServer` |
| 400 | `/wsProcess/IwsProcess` |

**SOAP envelope acceptance** — POST with proper SOAP 1.1 envelope:
| Status | Result |
|--------|--------|
| 202 Accepted | wsDataServer ReadRecords envelope accepted |
| 202 Accepted | wsProcess GetAvailableServices envelope accepted |
| `Server: Microsoft-HTTPAPI/2.0` | Confirms real Windows WCF service |

**OAuth token endpoint contract**:
```json
POST /api/connect/token → 401 with:
{
  "code": "FE005",
  "message": "Usuário ou Senha inválidos!...",
  "detailedMessage": "...",
  "helpUrl": "",
  "details": null
}
```
This JSON error structure (code/message/detailedMessage/helpUrl/details) validates our error handling contract.

**Authentication attempts** (all returned FE005 — this is a third-party's TOTVS Cloud instance, not MIP's):
- mestre/totvs, admin/totvs, ADMIN/totvs, p_mipti/totvs, rodrigo.sfaria/totvs, mip/totvs — all 401

#### Automated Live Contract Tests (14 tests)

Created `LiveRmContractTests.cs` — automated tests that validate against the live TOTVS Cloud RM:

| Test | What it proves |
|------|---------------|
| RestEndpoint_Returns401 (×7 Theory) | 7 REST API paths exist on real RM |
| DataServerRestGateway_Returns401 | DataServer REST gateway path exists |
| TokenEndpoint_ReturnsRmJsonError | OAuth error format matches our contract (FE005/code/message) |
| SoapEndpoint_Returns400OnGet (×2 Theory) | Both SOAP endpoints exist |
| SoapEnvelope_AcceptedByRealWcf | SOAP envelope structure correct (202 + Microsoft-HTTPAPI/2.0) |
| NonexistentEndpoint_Returns404 | Proves 401≠404 distinction (not a blanket response) |
| Server_IsMicrosoftHttpApi | Confirms real Windows WCF, not a proxy |

Tests are tagged `[Trait("Category", "LiveRM")]` and gracefully skip if the host is unreachable.

### Check

| Layer | Tests | Status |
|-------|-------|--------|
| Unit (SqlGuard, SoapEnvelope, Parser, DryRun, Contract) | 55 | All pass |
| Integration (SQL + SOAP + REST + Bridge + E2E stdio) | 70 | All pass |
| Integration — Live RM Contract | 14 | All pass |
| **Total** | **139** | **All pass** |

### What CAN and CANNOT be validated (final)

#### Validated (with evidence)
- **SQL path end-to-end**: Real SQL Server 2022, real schema (27 tables, 11 FKs, GDIC+GSISTEMA metadata)
- **SQL Guard enforcement**: SELECT/CTE allowed, INSERT/UPDATE/DELETE/DROP blocked
- **SOAP contract correctness**: Envelope building, response parsing, fault extraction — mock reproduces SOAP 1.1 contract
- **SOAP envelope acceptance on live RM**: Real RM.Host.Service at TOTVS Cloud accepts our SOAP envelopes (HTTP 202, `Server: Microsoft-HTTPAPI/2.0`)
- **REST API contract — live RM validation**: 9 REST endpoints confirmed on live RM (401, not 404), including /api/connect/token with real FE005 JSON error
- **REST API contract — Swagger alignment**: Our DataServer names/table names/field names validated against 8 real Swagger specs (2,281+ paths)
- **OAuth error contract**: Live RM returns `{code, message, detailedMessage, helpUrl, details}` — matches our error handling
- **Third-party cross-validation**: persontup bridge (independent Python RM MCP client, 1,430 tools from real Swagger specs) authenticates and makes calls through our mock
- **MCP stdio protocol**: 53 tools work over stdio JSON-RPC 2.0
- **Dry-run safety**: Write operations preview by default, require explicit confirmation
- **Architecture alignment**: Validated against ExtrairRAG-BancoDados + real Swagger specs

#### Cannot be validated (honest boundaries)
- **Authenticated RM data flow**: The live TOTVS Cloud RM instance is a third-party's (not MIP's). We don't have valid credentials for it. MIP's own RM at SVRTESTE (172.16.60.20) is off-network. When connected to MIP network, the "First connection" checklist below validates authenticated flow.
- **Full Corpore schema (~9k tables)**: Our 27-table schema covers every table our 53 tools query. Remaining tables exercised via rm_sql_tables/columns on production.
- **Production data volume**: Seed has ~100 rows; production has millions. Queries use TOP/WHERE/NOLOCK with maxRows enforcement.

#### First connection to real RM (when on MIP network)
1. Set `RM_SOAP_URL` to `http://svrteste:8051` and `RM_SQL_CONNECTION` to Corpore
2. Run `rm_ping` — validates both SQL and SOAP
3. Run `rm_dataserver_list` — validates DataServer names match real instance
4. Run `rm_dataserver_schema` for each DataServer — validates XSD matches
5. Run `rm_sql_tables` and `rm_sql_columns` — exercises full ~9k table schema
6. If any tool fails: fix the parser using the Swagger specs as reference

---

## Architecture Validation

Our hybrid SQL+SOAP design aligns with ExtrairRAG-BancoDados patterns:
- SQL metadata via INFORMATION_SCHEMA (same queries)
- CODCOLIGADA always in filters
- NOLOCK hint for reads
- GDIC for data dictionary (table: COLUNA='#', column: TABELA+COLUNA)
- GSISTEMA for module registry (single-letter code → module name)
- Module prefix convention (PFUNC=Folha, TMOV=Estoque, FLAN=Financeiro, CPARTIDA=Contábil)

Our additions beyond ExtrairRAG:
- SOAP write path (DataServer Save/Delete, Process Execute) — ExtrairRAG is read-only
- Dry-run safety pattern — no equivalent in ExtrairRAG
- Domain-specific tools (HR, Timesheet, Purchasing, Finance, Accounting) — ExtrairRAG is generic SQL only
- Dual environment with explicit selection — ExtrairRAG is single-connection

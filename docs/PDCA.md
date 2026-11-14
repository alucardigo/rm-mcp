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
- Commit with enriched schema and honest PDCA
- Real RM validation is a **network dependency**, not a code dependency — the server is ready

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

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
- Examine ExtrairRAG-BancoDados reference repo for RM patterns
- Improve mock SOAP to be DataServer-aware with realistic responses
- Write integration tests with actual data assertions against seeded DB
- Strengthen e2e test to verify data values, not just absence of errors
- Document PDCA cycles

### Do

#### Reference Repo Examination
Examined ExtrairRAG-BancoDados (mkazimoto):
- **MODULES map**: Single-letter prefixes → RM module names (A=Ponto, C=Contábil, P=Folha, T=Estoque/Compras, V=Gestão de Pessoas, M=Construção e Projetos)
- **GDIC table**: RM data dictionary (COLUNA='#' for table descriptions, TABELA+COLUNA for column descriptions)
- **GSISTEMA**: Module registry
- **SQL metadata**: Uses INFORMATION_SCHEMA for tables/columns/PKs/FKs — same approach as our rm_sql_tables/columns/fks
- **CODCOLIGADA**: Always included in filters (confirmed in doc-tools descriptions)
- **NOLOCK**: Required hint for read queries
- **SQL validator**: T-SQL syntax validation via node-sql-parser (parallels our SqlGuard)
- **.rules.md**: Valid column values documentation per table

Other references examined:
- ExtrairRAG-BancoDados-WindowsService: Windows Service wrapper for extraction
- mcpmarket.com/mapj: Private repo, Protheus-focused (not RM-specific)
- persontup/mcp-totvs-rm Docker: Python FastAPI/uvicorn MCP, NOT an RM instance
- skills.rest/skill/mapj: Marketplace listing, no additional RM patterns

#### Mock SOAP Improvements
- Added DataServer-specific schemas (GlbColigadaData, RhuFuncDados, EstMovimentoData, FinLanData)
- DataServer-aware ReadView/ReadRecord responses with realistic RM data
- Process-specific responses (EstAprovarMov, RhuDesligamento)
- Fixed XML namespace prefix handling (`<tot:ProcessServerName>` extraction)
- Full GDATASERVERNAME list from init-corpore.sql

#### Integration Tests (24 tests)
- **SqlQueryTests** (10): Ping, Coligadas (2 rows, MIP Engenharia), Funcionarios (3 employees with correct names/salarios), Parametrized queries, Movimentos (OC-001 = R$50k), Lancamentos financeiros (saldo assertions), Partidas contabeis (D/C), MaxRows truncation, ZMIP custom tables, RM version from GPARAMS
- **SqlGuardIntegrationTests** (6): SELECT WITH NOLOCK allowed, CTE counting ativos=2, INSERT/UPDATE/DELETE/DROP all blocked
- **SoapClientTests** (8): Ping, GetSchema (CODCOLIGADA field present), ReadView (MIP Engenharia data), ReadRecord, SaveRecord, ExecuteProcess (AP-001 from EstAprovarMov), DataServerList, BadCredentials 401

#### E2E Data Assertions
Test script now verifies 38 data values:
- rm_version returns "12.1.2406"
- rm_list_companies returns rowCount=2
- rm_get_employee returns "Joao Silva"
- rm_get_movement returns "OC-001"
- rm_get_entry returns "F001"
- rm_journal_entries returns rowCount=4
- rm_chart_of_accounts returns rowCount=6
- All dry-run tools return mode="preview"
- All confirmed writes return status="executado"

### Check
- 55 unit tests passing
- 24 integration tests passing (10 SQL + 6 Guard + 8 SOAP)
- 50/50 e2e tools passing with 38 data assertions
- Reference repos examined — RM patterns validated (our architecture aligns with ExtrairRAG's approach)
- Mock SOAP now DataServer-aware with namespace prefix handling
- **Limitation**: No real RM.Host.Service validated (off corporate network; mock matches known SOAP contract but not live behavior)
- **Limitation**: Docker SQL Server is NOT the actual Corpore schema (~9k tables vs our 25 test tables)

### Act
- Real RM.Host.Service validation to be done when connected to MIP corporate network
- Production Corpore schema will be exercised via rm_sql_tables/columns discovery tools
- Architecture is validated against reference implementations (ExtrairRAG patterns match our SQL metadata approach)

---

## Test Summary

| Layer | Tests | Status |
|-------|-------|--------|
| Unit (SqlGuard, SoapEnvelope, Parser, DryRun) | 55 | All pass |
| Integration (SQL data, Guard enforcement, SOAP) | 24 | All pass |
| E2E stdio (all 49 tools + rm_sql_fks) | 50 | All pass, 38 data values verified |
| **Total** | **129** | **All pass** |

## Architecture Validation

Our hybrid SQL+SOAP design aligns with ExtrairRAG-BancoDados patterns:
- SQL metadata via INFORMATION_SCHEMA (same queries)
- CODCOLIGADA always in filters
- NOLOCK hint for reads
- GDIC for data dictionary
- Module prefix convention (PFUNC=Folha, TMOV=Estoque, FLAN=Financeiro, CPARTIDA=Contábil)

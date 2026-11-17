# rm-mcp

MCP server + CLI para controle total do RM TOTVS (ERP) por agentes de IA.

Expõe **90 tools** via protocolo MCP (stdio) cobrindo SQL direto, SOAP DataServers (172 disponíveis), WCF net.tcp, REST, processos, fórmulas visuais e operações de domínio (RH, Financeiro, Contabilidade, Compras, Ponto, etc.). Inclui CLI companion para uso interativo no terminal.

## Canais de acesso

| Canal | Protocolo | Auth | Cobertura |
|-------|-----------|------|-----------|
| SQL direto | ADO.NET | Windows Auth | Leitura/escrita em qualquer tabela |
| HTTP SOAP | WCF/HTTP :8051 | Basic Auth | 172 DataServers (read/write/schema) |
| WCF net.tcp | WCF/TCP :8050 | ExecuteByPass | DataServers TCP + login sem senha |
| REST | HTTP :8051 | Basic Auth | Endpoints v1 (quando disponível) |

## Configuração

### Variáveis de ambiente (obrigatórias)

```bash
RM_SQL_CONNECTION="Server=MIP09\TESTE;Database=Corpore;Integrated Security=true;TrustServerCertificate=true"
RM_SOAP_URL="http://172.16.60.20:8051"
RM_SOAP_USER="mestre"
RM_SOAP_PASS="totvs"
```

### Variáveis opcionais

| Variável | Padrão | Descrição |
|----------|--------|-----------|
| `RM_WCF_HOST` | extraído de `RM_SOAP_URL` | Host do serviço WCF |
| `RM_WCF_PORT` | 8050 | Porta net.tcp |
| `RM_WCF_ALIAS` | CorporeRM | Alias do broker |
| `RM_WCF_USER` | mestre | Usuário para ExecuteByPass |

Para produção, adicione sufixo `_PROD` em todas as variáveis (`RM_SQL_CONNECTION_PROD`, etc.).

## Uso como MCP server

Sem argumentos, inicia o servidor MCP via stdio (JSON-RPC 2.0):

```bash
dotnet rm-mcp.dll
```

### Registro no Claude Code

```json
{
  "mcpServers": {
    "rm": {
      "command": "dotnet",
      "args": ["D:/Dev/rm-mcp/src/RmMcp.Server/bin/Debug/net10.0/rm-mcp.dll"],
      "env": {
        "RM_SQL_CONNECTION": "Server=MIP09\\TESTE;Database=Corpore;Integrated Security=true;TrustServerCertificate=true",
        "RM_SOAP_URL": "http://172.16.60.20:8051",
        "RM_SOAP_USER": "mestre",
        "RM_SOAP_PASS": "totvs",
        "DOTNET_ROOT": "C:/Program Files/dotnet"
      }
    }
  }
}
```

## Uso como CLI

### SQL direto

```bash
dotnet rm-mcp.dll query "SELECT TOP 5 NOMEFANTASIA FROM GCOLIGADA"
dotnet rm-mcp.dll query "SELECT CHAPA, NOME FROM PFUNC WHERE CODCOLIGADA=1" --max-rows 50
```

### DataServer (SOAP)

```bash
# Lê registros
dotnet rm-mcp.dll dataserver read FinCxaData --filter "CODCOLIGADA=1"

# Schema XML
dotnet rm-mcp.dll dataserver schema GlbColigadaData

# Conta registros
dotnet rm-mcp.dll dataserver count FopFuncionarioData --filter "CODCOLIGADA=1"

# Registro por chave primária
dotnet rm-mcp.dll dataserver record GlbColigadaData "1"
```

### Descoberta

```bash
# Lista todos os DataServers disponíveis
dotnet rm-mcp.dll list-dataservers

# Lista todas as 90 MCP tools
dotnet rm-mcp.dll --list
```

### Conectividade

```bash
# Testa SQL + SOAP + WCF
dotnet rm-mcp.dll ping

# Contra produção
dotnet rm-mcp.dll ping --env producao
```

## Tools MCP (90 total)

### SQL (6)
`rm_sql_query` `rm_sql_execute` `rm_sql_tables` `rm_sql_columns` `rm_sql_fks` `rm_sql_sample`

### DataServer HTTP SOAP (9)
`rm_dataserver_read` `rm_dataserver_read_paged` `rm_dataserver_record` `rm_dataserver_schema` `rm_dataserver_count` `rm_dataserver_save` `rm_dataserver_delete` `rm_dataserver_list` `rm_list_dataservers`

### WCF net.tcp (8)
`rm_wcf_login_bypass` `rm_wcf_broker_info` `rm_wcf_readview` `rm_wcf_readrecord` `rm_wcf_readschema` `rm_wcf_saverecord` `rm_wcf_deleterecord` `rm_wcf_generic`

### REST (6)
`rm_rest_get` `rm_rest_post` `rm_rest_put` `rm_rest_delete` `rm_rest_discover` `rm_rest_token`

### SOAP genérico (2)
`rm_soap_call` `rm_soap_raw`

### Consulta SQL SOAP (2)
`rm_consulta_sql` `rm_list_consultas_sql`

### Financeiro (5)
`rm_aging` `rm_cashflow` `rm_get_entry` `rm_list_entries` `rm_create_financial_entry`

### Contabilidade (3)
`rm_chart_of_accounts` `rm_journal_entries` `rm_trial_balance`

### RH (10)
`rm_employee_count` `rm_employee_history` `rm_get_employee` `rm_list_dependents` `rm_list_employees` `rm_list_positions` `rm_list_sections` `rm_payroll_summary` `rm_update_employee` `rm_terminate_employee`

### Ponto (4)
`rm_absences` `rm_clock_entries` `rm_overtime` `rm_timesheet`

### Compras (6)
`rm_get_movement` `rm_invoices` `rm_list_movements` `rm_list_products` `rm_list_suppliers` `rm_purchase_orders`

### Processos/Jobs (7)
`rm_run_formula` `rm_process_execute` `rm_process_status` `rm_execute_job` `rm_job_history` `rm_list_jobs` `rm_approve_movement`

### Global (5)
`rm_list_companies` `rm_list_branches` `rm_list_cost_centers` `rm_list_users` `rm_custom_tables`

### Escrita (2)
`rm_create_movement` `rm_generate_report`

### Meta/Sistema (8)
`rm_ping` `rm_version` `rm_services` `rm_aliases` `rm_system_version` `rm_list_processes` `rm_list_formulas` `rm_list_dataservers`

### Segurança (4)
`rm_user_security` `rm_list_profiles` `rm_list_user_permissions` `rm_user_profiles`

### Arquivos/E-mail (4)
`rm_list_attachments` `rm_list_reports` `rm_mail_queue` `rm_send_mail`

## Arquitetura

```
RmMcp.Server          Entry point (MCP stdio + CLI)
RmMcp.Tools           90 MCP tools organizados por domínio
RmMcp.Core            Config, enums, tipos compartilhados
RmMcp.Sql             SqlQueryExecutor + SqlGuard (read-only)
RmMcp.Soap            RmSoapClient (HTTP), RmWcfClient (net.tcp), RmRestClient
```

Target: `net10.0` | UseAppHost: `false` (roda via `dotnet rm-mcp.dll`)

## Build

```bash
dotnet build
```

## Testes

```bash
# Unitários
dotnet test tests/RmMcp.Tests.Unit

# Integração (requer Docker ou conexão com RM)
dotnet test tests/RmMcp.Tests.Integration
```

## Notas técnicas

- **HTTP SOAP** requer que `GPARAMS.VERSAOBASE` no banco Corpore corresponda à versão real do binário do RM.Host.Service. Mismatch causa 401.
- **WCF ExecuteByPass** dispensa senha — o erro `ParamsInvalidCRC` é informacional (o principal ainda é retornado).
- **GZip** é obrigatório no SVRTESTE (`IsCompressionEnabled=true`). O client WCF já inclui o encoder.
- **Senha** usa BCrypt.Net 4.0.2 com prefixo `$2a$` (não `$2b$` do Python bcrypt). O CRC do GUSUARIO.CONTROLE usa algoritmo CRC-CCITT polinomial.
- **Cache de GPARAMS**: TTL de 10 minutos. Após alterar valores no banco, aguardar antes de testar.

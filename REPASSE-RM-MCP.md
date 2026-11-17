# Repasse — RM MCP: Controle Total do RM TOTVS via Agente IA

**Data**: 17/11/2026
**Autor**: Rodrigo S. Faria (MIP Engenharia)
**Propósito**: Documento de transferência para ensinar um agente Claude a usar o RM MCP,
o RM Recorder e o Replicant RM como ferramentas integradas de automação do ERP TOTVS RM.

---

## O Que É

O **RM MCP** é um MCP server (Model Context Protocol) + CLI que dá a um agente de IA
acesso completo ao TOTVS RM — o ERP que a MIP Engenharia usa para financeiro, RH,
contabilidade, compras, ponto e processos. São **90 tools MCP** cobrindo:

- SQL direto no banco Corpore (leitura/escrita)
- 172 DataServers HTTP SOAP (a API nativa do RM)
- WCF net.tcp (login sem senha via ExecuteByPass)
- REST (quando disponível)
- Operações de domínio prontas (folha, lançamentos, movimentos, etc.)

Ele é parte de um ecossistema de 3 ferramentas:

| Ferramenta | O que faz | Repo |
|---|---|---|
| **RM MCP** | Acesso programático ao RM (MCP + CLI) | `alucardigo/rm-mcp` |
| **RM Recorder** | Grava fluxos feitos no RM e exporta como teste | `alucardigo/rm-recorder` |
| **Replicant RM** | Reproduz/automatiza os testes gravados (substitui TOTVS TestFlow) | `alucardigo/replicant-rm` |

---

## Configuração do MCP Server

### Registro no Claude Code (`.mcp.json`)

```json
{
  "mcpServers": {
    "rm-mcp": {
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

### Variáveis de Ambiente

| Variável | Obrigatória | Descrição |
|---|---|---|
| `RM_SQL_CONNECTION` | Sim | Connection string ADO.NET para o banco Corpore |
| `RM_SOAP_URL` | Sim | URL base do RM Host (ex: `http://172.16.60.20:8051`) |
| `RM_SOAP_USER` | Sim | Usuário para Basic Auth (ex: `mestre`) |
| `RM_SOAP_PASS` | Sim | Senha (ex: `totvs`) |
| `RM_WCF_HOST` | Não | Host WCF net.tcp (default: extraído de RM_SOAP_URL) |
| `RM_WCF_PORT` | Não | Porta WCF (default: 8050) |
| `RM_WCF_ALIAS` | Não | Alias do broker (default: CorporeRM) |
| `RM_WCF_USER` | Não | Usuário WCF ByPass (default: mestre) |
| `DOTNET_ROOT` | Sim no Windows | Caminho do .NET SDK (ex: `C:/Program Files/dotnet`) |

Para produção: sufixo `_PROD` em todas (`RM_SQL_CONNECTION_PROD`, etc.).

---

## 3 Caminhos de Acesso ao RM

O RM MCP usa 3 caminhos complementares. Cada um tem suas forças:

### 1. SQL Direto (ADO.NET/TDS)

**Quando usar**: consultas rápidas, relatórios, escrita direta em tabelas, qualquer
coisa que não precise passar pela camada de negócio do RM.

- Sem limitação de DataServer
- Acesso a TODAS as tabelas do Corpore (593 GB)
- Read e write (com cuidado — sem validação de regra de negócio do RM)
- Auth: Windows Integrated (rm / Totvs$2211 no MIP09\TESTE)

**Tools**: `rm_sql_query`, `rm_sql_execute`, `rm_sql_tables`, `rm_sql_columns`,
`rm_sql_fks`, `rm_sql_sample`

**Exemplo**:
```
rm_sql_query("SELECT TOP 10 CHAPA, NOME FROM PFUNC WHERE CODCOLIGADA=1")
```

### 2. HTTP SOAP (DataServers — porta 8051)

**Quando usar**: operações que precisam respeitar as regras de negócio do RM
(validações, triggers de processo, cálculos). É o caminho "oficial" da API do RM.

- 172 DataServers disponíveis (Fin*, Glb*, Rhu*, Ctb*, Cmp*, etc.)
- Read, Write, Delete, Schema, Count
- Auth: Basic Auth (mestre/totvs)
- Respeita regras de negócio e permissões do RM

**Tools**: `rm_dataserver_read`, `rm_dataserver_read_paged`, `rm_dataserver_record`,
`rm_dataserver_schema`, `rm_dataserver_count`, `rm_dataserver_save`,
`rm_dataserver_delete`, `rm_dataserver_list`, `rm_list_dataservers`

**Exemplo**:
```
rm_dataserver_read("FinCxaData", filter="CODCOLIGADA=1")
rm_dataserver_schema("GlbColigadaData")
```

### 3. WCF net.tcp (porta 8050 — ExecuteByPass)

**Quando usar**: login sem senha (automação pura), acesso aos 2 DataServers TCP
(FinCxaData, CtbCCustoData). Útil para auth quando HTTP falha.

- Login via `ExecuteByPass` (sem senha, pula version check)
- Poucos DataServers disponíveis via TCP (a maioria é HTTP-only)
- Auth: transport-level ("rm sistemas"/"rm sistemas", hardcoded no RM)
- GZip obrigatório (encoder incluso)

**Tools**: `rm_wcf_login_bypass`, `rm_wcf_broker_info`, `rm_wcf_readview`,
`rm_wcf_readrecord`, `rm_wcf_readschema`, `rm_wcf_saverecord`,
`rm_wcf_deleterecord`, `rm_wcf_generic`

**Exemplo**:
```
rm_wcf_login_bypass()
rm_wcf_readview("FinCxaData", filter="CODCOLIGADA=1")
```

---

## Catálogo Completo de Tools (90)

### SQL (6)
| Tool | O que faz |
|------|-----------|
| `rm_sql_query` | Executa SELECT e retorna resultados |
| `rm_sql_execute` | Executa INSERT/UPDATE/DELETE (retorna rows affected) |
| `rm_sql_tables` | Lista tabelas do banco (com filtro opcional) |
| `rm_sql_columns` | Lista colunas de uma tabela |
| `rm_sql_fks` | Lista foreign keys de uma tabela |
| `rm_sql_sample` | Retorna amostra de dados de uma tabela |

### DataServer HTTP SOAP (9)
| Tool | O que faz |
|------|-----------|
| `rm_dataserver_read` | Lê registros de um DataServer (com filtro) |
| `rm_dataserver_read_paged` | Lê com paginação (offset/limit) |
| `rm_dataserver_record` | Lê um registro por chave primária |
| `rm_dataserver_schema` | Retorna schema XSD do DataServer |
| `rm_dataserver_count` | Conta registros (com filtro) |
| `rm_dataserver_save` | Salva/atualiza registro via DataServer |
| `rm_dataserver_delete` | Deleta registro via DataServer |
| `rm_dataserver_list` | Lista DataServers registrados no RM |
| `rm_list_dataservers` | Alias para listar DataServers |

### WCF net.tcp (8)
| Tool | O que faz |
|------|-----------|
| `rm_wcf_login_bypass` | Login sem senha via ExecuteByPass |
| `rm_wcf_broker_info` | Informações do broker WCF |
| `rm_wcf_readview` | ReadView via WCF (lista de registros) |
| `rm_wcf_readrecord` | ReadRecord via WCF (registro único) |
| `rm_wcf_readschema` | GetSchema via WCF |
| `rm_wcf_saverecord` | SaveRecord via WCF |
| `rm_wcf_deleterecord` | DeleteRecord via WCF |
| `rm_wcf_generic` | Chamada WCF genérica |

### REST (6)
| Tool | O que faz |
|------|-----------|
| `rm_rest_get` | GET em endpoint REST do RM |
| `rm_rest_post` | POST em endpoint REST |
| `rm_rest_put` | PUT em endpoint REST |
| `rm_rest_delete` | DELETE em endpoint REST |
| `rm_rest_discover` | Descobre endpoints REST disponíveis |
| `rm_rest_token` | Obtém token de autenticação REST |

### SOAP Genérico (2)
| Tool | O que faz |
|------|-----------|
| `rm_soap_call` | Chamada SOAP tipada a qualquer serviço RM |
| `rm_soap_raw` | Envelope SOAP raw (máximo controle) |

### Consulta SQL SOAP (2)
| Tool | O que faz |
|------|-----------|
| `rm_consulta_sql` | Executa Consulta SQL cadastrada no RM |
| `rm_list_consultas_sql` | Lista Consultas SQL disponíveis |

### Financeiro (5)
| Tool | O que faz |
|------|-----------|
| `rm_aging` | Aging de contas a receber/pagar |
| `rm_cashflow` | Fluxo de caixa |
| `rm_get_entry` | Busca lançamento financeiro |
| `rm_list_entries` | Lista lançamentos financeiros |
| `rm_create_financial_entry` | Cria lançamento financeiro |

### Contabilidade (3)
| Tool | O que faz |
|------|-----------|
| `rm_chart_of_accounts` | Plano de contas |
| `rm_journal_entries` | Lançamentos contábeis |
| `rm_trial_balance` | Balancete |

### RH (10)
| Tool | O que faz |
|------|-----------|
| `rm_employee_count` | Conta funcionários |
| `rm_employee_history` | Histórico do funcionário |
| `rm_get_employee` | Busca funcionário |
| `rm_list_dependents` | Lista dependentes |
| `rm_list_employees` | Lista funcionários |
| `rm_list_positions` | Lista cargos/funções |
| `rm_list_sections` | Lista seções/departamentos |
| `rm_payroll_summary` | Resumo de folha |
| `rm_update_employee` | Atualiza cadastro |
| `rm_terminate_employee` | Processa demissão |

### Ponto (4)
| Tool | O que faz |
|------|-----------|
| `rm_absences` | Afastamentos |
| `rm_clock_entries` | Batidas de ponto |
| `rm_overtime` | Horas extras |
| `rm_timesheet` | Espelho de ponto |

### Compras (6)
| Tool | O que faz |
|------|-----------|
| `rm_get_movement` | Busca movimento |
| `rm_invoices` | Notas fiscais |
| `rm_list_movements` | Lista movimentos |
| `rm_list_products` | Lista produtos |
| `rm_list_suppliers` | Lista fornecedores |
| `rm_purchase_orders` | Pedidos de compra |

### Processos/Jobs (7)
| Tool | O que faz |
|------|-----------|
| `rm_run_formula` | Executa fórmula visual |
| `rm_process_execute` | Executa processo RM |
| `rm_process_status` | Status de processo |
| `rm_execute_job` | Dispara job agendado |
| `rm_job_history` | Histórico de jobs |
| `rm_list_jobs` | Lista jobs |
| `rm_approve_movement` | Aprova movimento (workflow) |

### Global (5)
| Tool | O que faz |
|------|-----------|
| `rm_list_companies` | Lista coligadas |
| `rm_list_branches` | Lista filiais |
| `rm_list_cost_centers` | Lista centros de custo |
| `rm_list_users` | Lista usuários do RM |
| `rm_custom_tables` | Tabelas complementares |

### Escrita (2)
| Tool | O que faz |
|------|-----------|
| `rm_create_movement` | Cria movimento (compra/venda/transferência) |
| `rm_generate_report` | Gera relatório do RM |

### Meta/Sistema (8)
| Tool | O que faz |
|------|-----------|
| `rm_ping` | Testa conectividade (SQL + SOAP + WCF) |
| `rm_version` | Versão do RM MCP |
| `rm_services` | Serviços WCF disponíveis |
| `rm_aliases` | Aliases configurados |
| `rm_system_version` | Versão do RM Host |
| `rm_list_processes` | Processos disponíveis no RM |
| `rm_list_formulas` | Fórmulas visuais |
| `rm_list_dataservers` | DataServers disponíveis |

### Segurança (4)
| Tool | O que faz |
|------|-----------|
| `rm_user_security` | Perfil de segurança de um usuário |
| `rm_list_profiles` | Perfis de segurança |
| `rm_list_user_permissions` | Permissões de um usuário |
| `rm_user_profiles` | Perfis atribuídos a um usuário |

### Arquivos/E-mail (4)
| Tool | O que faz |
|------|-----------|
| `rm_list_attachments` | Anexos de um registro |
| `rm_list_reports` | Relatórios disponíveis |
| `rm_mail_queue` | Fila de e-mails do RM |
| `rm_send_mail` | Envia e-mail via RM |

---

## CLI Companion

O mesmo binário funciona como CLI quando recebe argumentos:

```bash
# SQL direto
dotnet rm-mcp.dll query "SELECT TOP 5 NOMEFANTASIA FROM GCOLIGADA"
dotnet rm-mcp.dll query "SELECT CHAPA, NOME FROM PFUNC WHERE CODCOLIGADA=1" --max-rows 50

# DataServer
dotnet rm-mcp.dll dataserver read FinCxaData --filter "CODCOLIGADA=1"
dotnet rm-mcp.dll dataserver schema GlbColigadaData
dotnet rm-mcp.dll dataserver count FopFuncionarioData --filter "CODCOLIGADA=1"
dotnet rm-mcp.dll dataserver record GlbColigadaData "1"
dotnet rm-mcp.dll ds read FinCxaData   # alias: ds = dataserver

# Descoberta
dotnet rm-mcp.dll list-dataservers     # Lista os 172 DataServers
dotnet rm-mcp.dll --list               # Lista as 90 MCP tools

# Conectividade
dotnet rm-mcp.dll ping                 # Testa SQL + SOAP + WCF
dotnet rm-mcp.dll ping --env producao  # Contra produção
```

---

## Padrões de Uso Comuns

### 1. Consultar dados financeiros
```
rm_sql_query("SELECT TOP 20 * FROM FLAN WHERE CODCOLIGADA=1 ORDER BY DATAVENCIMENTO DESC")
```

### 2. Listar funcionários ativos
```
rm_list_employees(codcoligada=1, active_only=true)
```

### 3. Buscar DataServer e ler dados
```
rm_list_dataservers()  → encontra "FinLanData"
rm_dataserver_read("FinLanData", filter="CODCOLIGADA=1 AND STATUSLAN=1")
```

### 4. Verificar conectividade
```
rm_ping()  → retorna status de SQL, SOAP e WCF
```

### 5. Explorar schema de uma tabela
```
rm_sql_columns("PFUNC")     → colunas da tabela
rm_sql_fks("PFUNC")         → foreign keys
rm_dataserver_schema("FopFuncionarioData")  → schema XML do DataServer
```

### 6. Executar processo do RM
```
rm_list_processes()          → encontra o processo
rm_process_execute(processId="...", params={...})
rm_process_status(executionId="...")
```

### 7. Write-flow completo (Read → Modify → Save → Verify)
```
rm_dataserver_record("FinCxaData", key="1;1540011")   → lê registro
# ... modifica campos no XML
rm_dataserver_save("FinCxaData", xml=modified_xml)      → salva
rm_dataserver_record("FinCxaData", key="1;1540011")   → verifica
```

---

## Gotchas e Notas Técnicas

1. **Chaves primárias do RM usam ponto-e-vírgula**: `"1;1540011"` (CODCOLIGADA;IDCXA).
   Nunca separar por vírgula.

2. **STATUSLAN**: O RM bloqueia save quando `STATUSLAN != 1`. Verificar antes de
   tentar alterar lançamentos financeiros.

3. **GZip obrigatório**: O WCF no SVRTESTE tem `IsCompressionEnabled=true`. O client
   já inclui o encoder. Se criar chamadas SOAP manuais, incluir o header de GZip.

4. **Version mismatch**: O HTTP SOAP requer que `GPARAMS.VERSAOBASE` no banco bata com
   a versão do binário RM.Host.Service. Mismatch causa 401. O WCF ExecuteByPass pula
   essa verificação.

5. **CRC de usuário**: Se alterar GUSUARIO.SENHA via SQL direto, o campo CONTROLE
   precisa ser recalculado com algoritmo CRC-CCITT polinomial (não é CRC32 padrão).
   Usar RmHashTool (.NET) para gerar hash + CRC corretos.

6. **BCrypt**: O RM usa BCrypt.Net 4.0.2 com prefixo `$2a$` (NÃO `$2b$` do Python).
   Hash Python gerará CRC diferente e login falhará.

7. **Cache de GPARAMS**: TTL de 10 minutos. Após alterar valores no banco, aguardar.

8. **SQL Guard**: As tools SQL do MCP têm proteção: `rm_sql_query` só aceita SELECT.
   Para escrita, use `rm_sql_execute` que aceita INSERT/UPDATE/DELETE (mas não DROP/TRUNCATE).

9. **DataServer read retorna XML**: O formato nativo é XML do RM. Os campos são
   elements, não atributos. Use XPath para navegar.

---

## Ferramentas Complementares

### RM Recorder (`alucardigo/rm-recorder`)

Grava interações feitas no RM (trocas SOAP, queries SQL) e exporta como:
- `.replicant.json` — test case importável pelo Replicant RM
- `.trec.xml` — formato compatível com TOTVS TestFlow

Comandos: `start`, `stop`, `replay`, `export`, `list-dataservers`, `attach`

### Replicant RM (`alucardigo/replicant-rm`)

Substituto open-source do TOTVS TestFlow. Reproduz fluxos gravados pelo Recorder:
- DAG execution (topological sort, edges Success/Failure/Always)
- 12 tipos de activity (RM SOAP, HTTP, SQL, Selenium, Script, WinForms, etc.)
- CLI mode: `run`, `import`, `list`, `status` (sem banco externo, usa SQLite)
- Web mode: REST API + MCP + Hangfire (precisa de PostgreSQL)

---

## Ambiente de Teste

| Recurso | Endereço | Credenciais |
|---|---|---|
| RM Host HTTP SOAP | `http://172.16.60.20:8051` | mestre / totvs |
| RM Host WCF TCP | `net.tcp://172.16.60.20:8050` | ExecuteByPass (sem senha) |
| SQL Server (teste) | `MIP09\TESTE` | Windows Auth (Integrated Security) |
| Banco | `Corpore` | — |

O servidor `172.16.60.20` (SVRTESTE) é um ambiente de teste Azure com RM.Host.Service,
Fluig, Engeman e outros serviços. Acesso via VPN da rede MIP (dmip.local).

---

## Resumo para o Agente

Quando te pedirem algo relacionado ao RM TOTVS:

1. **Sempre comece com `rm_ping`** para verificar se SQL, SOAP e WCF estão respondendo
2. **Prefira DataServer** sobre SQL direto para operações que precisam de regra de negócio
3. **Prefira SQL direto** para consultas de leitura rápida e relatórios
4. **Use `rm_list_dataservers`** quando não souber qual DataServer contém o dado
5. **Use `rm_dataserver_schema`** para entender a estrutura antes de ler/escrever
6. **Chaves primárias** são separadas por ponto-e-vírgula, não vírgula
7. **Para escrita via DataServer**: leia o registro primeiro, modifique o XML, e salve
8. **WCF ExecuteByPass** é o fallback quando HTTP SOAP falha por version mismatch

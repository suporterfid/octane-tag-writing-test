# Octane Tag Writing Test

Um sistema abrangente de testes .NET 8 para avaliação de operações de escrita em tags RFID utilizando o Impinj Octane SDK. Este projeto implementa múltiplas estratégias de teste para avaliar diferentes aspectos de performance, confiabilidade e funcionalidade de escrita em tags RFID.

## Visão Geral

Esta aplicação fornece um framework estruturado de testes para operações de escrita em tags RFID, implementando várias estratégias de teste usando o padrão Strategy para organizar diferentes cenários de teste. A arquitetura facilita a adição de novos casos de teste mantendo uma interface consistente.

## Pré-requisitos

- .NET 8.0 SDK
- Impinj Octane SDK (v5.0.0)
- LLRP SDK (incluído com o Octane SDK)
- Leitor RFID Impinj (hostname/endereço IP necessário)
- Docker (opcional, para execução containerizada)

## Estrutura do Projeto

```
OctaneTagWritingTest/
├── JobStrategies/
│   ├── JobStrategy0ReadOnlyLogging.cs          # Logging apenas de leitura
│   ├── JobStrategy1SpeedStrategy.cs            # Teste de velocidade de escrita
│   ├── JobStrategy2MultiAntennaWriteStrategy.cs # Escrita multi-antena
│   ├── JobStrategy3BatchSerializationPermalockStrategy.cs # Operações em lote
│   ├── JobStrategy4VerificationCycleStrategy.cs # Ciclo de verificação
│   ├── JobStrategy5EnduranceStrategy.cs        # Teste de resistência
│   ├── JobStrategy6RobustnessStrategy.cs       # Teste de robustez
│   ├── JobStrategy7OptimizedStrategy.cs        # Estratégia otimizada
│   ├── JobStrategy8MultipleReaderEnduranceStrategy.cs # Resistência multi-leitor
│   └── JobStrategy9CheckBox.cs                 # Teste CheckBox
├── Helpers/
│   ├── EpcListManager.cs           # Gerenciamento de listas EPC
│   ├── TagOpController.cs          # Controle de operações de tags
│   ├── CommandLineParser.cs        # Parser de linha de comando
│   └── InteractiveConfig.cs        # Configuração interativa
├── BaseTestStrategy.cs             # Classe base para estratégias
├── IJobStrategy.cs                 # Interface do padrão Strategy
├── JobManager.cs                   # Gerenciador de execução de testes
├── Program.cs                      # Ponto de entrada da aplicação
├── ApplicationConfig.cs            # Configuração da aplicação
├── ReaderSettings.cs               # Configurações do leitor
├── ReaderSettingsManager.cs        # Gerenciador de configurações
└── Dockerfile                      # Configuração Docker
```

## Configuração do Projeto

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>
</Project>
```

### Pacotes NuGet
- OctaneSDK (v5.0.0)
- Newtonsoft.Json (v13.0.3)
- Microsoft.VisualStudio.Azure.Containers.Tools.Targets (v1.21.0)

## Estratégias de Teste Implementadas

### 0. Read-Only Logging (JobStrategy0)
- Apenas logging de operações de leitura
- Não realiza escrita, apenas monitora tags

### 1. Speed Strategy (JobStrategy1)
- Otimizado para máxima velocidade de escrita
- Mede e registra timing das operações de escrita
- Resultados salvos em arquivo CSV

### 2. Multi-Antenna Write Strategy (JobStrategy2)
- Testa escrita através de múltiplas antenas
- Avalia coordenação e troca de antenas

### 3. Batch Serialization Permalock Strategy (JobStrategy3)
- Testa operações de escrita em lote
- Avalia performance de serialização
- Inclui operações de permalock

### 4. Verification Cycle Strategy (JobStrategy4)
- Implementa ciclos de escrita-verificação
- Garante integridade dos dados escritos

### 5. Endurance Strategy (JobStrategy5)
- Teste de resistência/durabilidade
- Executa múltiplos ciclos de escrita

### 6. Robustness Strategy (JobStrategy6)
- Teste de robustez com retry logic
- Implementa recuperação de erros
- Máximo de 5 tentativas por operação

### 7. Optimized Strategy (JobStrategy7)
- Estratégia otimizada para performance
- Configurações ajustadas para melhor throughput

### 8. Multiple Reader Endurance Strategy (JobStrategy8)
- **Estratégia avançada com múltiplos leitores**
- Utiliza três leitores: detector, writer e verifier
- Detector: monitora tags no campo
- Writer: executa operações de escrita
- Verifier: verifica integridade dos dados escritos
- Suporte a GPI para sincronização
- Logging detalhado com timestamps e métricas

### 9. CheckBox Strategy (JobStrategy9)
- Teste específico para funcionalidades CheckBox
- Validação de SKUs específicos

## Configuração e Execução

### Linha de Comando

```bash
# Execução básica com dois leitores
OctaneTagWritingTest.exe [detector-hostname] [writer-hostname]

# Execução com três leitores (detector, writer, verifier)
OctaneTagWritingTest.exe [detector-hostname] [writer-hostname] [verifier-hostname]

# Modo interativo
OctaneTagWritingTest.exe --interactive

# Ajuda
OctaneTagWritingTest.exe --help
```

### Modo Interativo

O aplicativo suporta configuração interativa quando executado sem parâmetros ou com a flag `--interactive`:

```bash
OctaneTagWritingTest.exe -i
```

### Configuração de Leitores

O sistema utiliza `ApplicationConfig` para gerenciar configurações centralizadas:

- **DetectorHostname**: Leitor responsável por detectar tags
- **WriterHostname**: Leitor responsável por operações de escrita
- **VerifierHostname**: Leitor responsável por verificação

Cada leitor pode ter configurações específicas:
- Potência de transmissão (TxPowerInDbm)
- Sensibilidade de recepção (RxSensitivityInDbm)
- Modo de busca (SearchMode)
- Sessão (Session)
- Modo RF (RfMode)

### Arquivos de Configuração

O sistema salva configurações em arquivos JSON na pasta `reader_settings/`:
- `detector.json`: Configurações do leitor detector
- `writer.json`: Configurações do leitor writer
- `verifier.json`: Configurações do leitor verifier

## Docker

### Build da Imagem

```bash
# Build para produção
docker build -t octane-tag-writing-test .

# Build para debug
docker build --build-arg BUILD_CONFIGURATION=Debug -t octane-tag-writing-test .
```

### Execução

```bash
# Execução em modo produção
docker run octane-tag-writing-test [detector-hostname] [writer-hostname] [verifier-hostname]

# Execução com configurações específicas
docker run octane-tag-writing-test 192.168.68.248 192.168.68.94 192.168.68.100
```

### Otimizações Docker

O projeto inclui `.dockerignore` para otimizar o contexto de build:
- Artefatos de desenvolvimento (bin/, obj/)
- Arquivos de controle de versão (.git/, .gitignore)
- Arquivos de IDE (.vs/, .vscode/, *.user)
- Arquivos de configuração sensíveis

## Sistema de Logging

Cada estratégia de teste gera arquivos de log CSV com métricas relevantes:

### Campos Padrão dos Logs
- **Timestamp**: Data/hora da operação
- **TID**: Tag ID
- **Previous_EPC**: EPC anterior
- **New_EPC/Expected_EPC**: Novo EPC ou EPC esperado
- **WriteTime_ms**: Tempo de escrita em milissegundos
- **VerifyTime_ms**: Tempo de verificação em milissegundos
- **Result**: Resultado da operação (Success/Failure)
- **RSSI**: Força do sinal
- **AntennaPort**: Porta da antena utilizada
- **ChipModel**: Modelo do chip da tag

### Arquivos de Log por Estratégia
- `TestCase0_ReadOnlyLogging_Log-[description].csv`
- `TestCase1_Log-SpeedStrategy-[description].csv`
- `TestCase3_MultiAntenna_Log-[description].csv`
- `TestCase4_VerificationCycle_Log-[description].csv`
- `TestCase6_Robustness_Log-[description].csv`
- `TestCase7_Log-OptimizedStrategy-[description].csv`
- `TestCase8_Log-DualReaderEnduranceStrategy-[description].csv`
- `TestCase9_Log-CheckBox-[description].csv`

## Arquitetura e Padrões

### Padrão Strategy
- `IJobStrategy`: Interface base para todas as estratégias
- `BaseTestStrategy`: Classe base com funcionalidades comuns
- Estratégias específicas implementam `IJobStrategy`

### Gerenciamento de Configurações
- `ApplicationConfig`: Configuração centralizada da aplicação
- `ReaderSettings`: Configurações específicas por leitor
- `ReaderSettingsManager`: Singleton para gerenciar configurações

### Funcionalidades Comuns
- Conexão e gerenciamento de leitores
- Configurações padrão otimizadas
- Relatórios de baixa latência
- Gerenciamento de listas EPC
- Logging estruturado em CSV
- Suporte a cancelamento via CancellationToken

## Desenvolvimento

### Adicionando Nova Estratégia

1. Crie uma nova classe na pasta `JobStrategies/`
2. Implemente a interface `IJobStrategy`
3. Herde de `BaseTestStrategy` para funcionalidades comuns
4. Implemente o método `RunJob(CancellationToken)`
5. Registre a estratégia no `JobManager.cs`

```csharp
public class JobStrategyCustom : BaseTestStrategy
{
    public JobStrategyCustom(string hostname, string logFile, Dictionary<string, ReaderSettings> readerSettings)
        : base(hostname, logFile, readerSettings)
    {
    }

    public override void RunJob(CancellationToken cancellationToken = default)
    {
        // Implementar lógica do teste
    }
}
```

### Configuração de Desenvolvimento

O projeto suporta configuração automática em modo debug:
- Detecção automática de modo debug
- Configuração interativa quando sem argumentos
- Settings pré-configurados para desenvolvimento

## Dependências Externas

### TagUtils
Projeto auxiliar para utilitários de tags RFID, incluído como dependência do projeto principal.

### EpcListGenerator
Utilitário separado para geração de listas EPC, com seu próprio Dockerfile e configurações.

## Características Avançadas

### Estratégia 8 - Multiple Reader Endurance
Esta estratégia implementa um sistema avançado com três leitores:

```csharp
// Configuração da estratégia 8
strategies.Add("8", new JobStrategy8MultipleReaderEnduranceStrategy(
    hostnameDetector,    // Leitor detector
    hostnameWriter,      // Leitor writer  
    hostnameVerifier,    // Leitor verifier
    logFile,
    readerSettings,
    applicationConfig    // Configuração centralizada
));
```

#### Funcionalidades:
- **Detecção contínua**: Leitor detector monitora tags no campo
- **Escrita coordenada**: Leitor writer executa operações de escrita
- **Verificação automática**: Leitor verifier valida dados escritos
- **Sincronização GPI**: Suporte a General Purpose Input para coordenação
- **Retry automático**: Re-escrita automática em caso de discrepância
- **Métricas detalhadas**: Logging completo de timing e resultados

### Suporte a SKU
Estratégias específicas suportam validação por SKU:
```csharp
strategies.Add("9", new JobStrategy9CheckBox(hostname, logFile, readerSettings, sku));
```

## Troubleshooting

### Problemas Comuns

1. **Conexão com leitor falha**
   - Verifique conectividade de rede
   - Confirme hostname/IP do leitor
   - Verifique se o leitor está configurado corretamente

2. **Logs não são gerados**
   - Verifique permissões de escrita
   - Confirme se o diretório de saída existe

3. **Performance baixa**
   - Ajuste configurações de potência e sensibilidade
   - Considere usar estratégias otimizadas (Strategy 7)

### Debug

Execute em modo debug para informações detalhadas:
```bash
# No Visual Studio
F5 para debug com breakpoints

# Via linha de comando (se compilado em Debug)
OctaneTagWritingTest.exe --debug [argumentos]
```

## Licença

[Especificar Licença]

## Contribuição

Para contribuir com o projeto:
1. Faça fork do repositório
2. Crie uma branch para sua feature
3. Implemente e teste suas mudanças
4. Submeta um pull request

## Notas

- Certifique-se da conectividade adequada do leitor antes de executar testes
- Revise a documentação individual de cada estratégia para requisitos específicos
- Verifique os arquivos de log CSV para resultados detalhados dos testes
- Use o modo interativo para configuração inicial e testes
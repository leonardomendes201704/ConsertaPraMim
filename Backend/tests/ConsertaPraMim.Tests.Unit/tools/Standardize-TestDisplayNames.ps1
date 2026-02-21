param(
    [string]$RootPath = (Join-Path $PSScriptRoot ".."),
    [switch]$Apply,
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $Apply -and -not $Check) {
    throw "Use -Apply para escrever alteracoes ou -Check para validar."
}

$wordMap = @{
    "a" = ""
    "an" = ""
    "and" = "e"
    "api" = "api"
    "are" = ""
    "as" = "como"
    "async" = ""
    "attachment" = "anexo"
    "attachments" = "anexos"
    "auth" = "autenticacao"
    "bad" = "invalida"
    "be" = ""
    "by" = "por"
    "can" = "pode"
    "cancel" = "cancelar"
    "cannot" = "nao pode"
    "chat" = "chat"
    "class" = "classe"
    "client" = "cliente"
    "close" = "fechar"
    "conflict" = "conflito"
    "controller" = "controller"
    "create" = "criar"
    "created" = "criado"
    "credit" = "credito"
    "credits" = "creditos"
    "data" = "dados"
    "delete" = "excluir"
    "delivered" = "entregue"
    "does" = ""
    "e2e" = "e2e"
    "empty" = "vazio"
    "error" = "erro"
    "exists" = "existe"
    "fail" = "falhar"
    "failed" = "falha"
    "fails" = "falha"
    "false" = "falso"
    "forbidden" = "proibido"
    "found" = "encontrado"
    "from" = "de"
    "get" = "obter"
    "has" = "tem"
    "have" = "tem"
    "history" = "historico"
    "id" = "id"
    "if" = "se"
    "in" = "em"
    "inmemory" = "in-memory"
    "integration" = "integracao"
    "invalid" = "invalido"
    "is" = ""
    "json" = "json"
    "list" = "listar"
    "login" = "login"
    "mark" = "marcar"
    "message" = "mensagem"
    "messages" = "mensagens"
    "mobile" = "mobile"
    "not" = "nao"
    "notification" = "notificacao"
    "notifications" = "notificacoes"
    "null" = "nulo"
    "ok" = "ok"
    "open" = "abrir"
    "or" = "ou"
    "order" = "pedido"
    "orders" = "pedidos"
    "payload" = "payload"
    "persist" = "persistir"
    "policy" = "politica"
    "provider" = "prestador"
    "read" = "lido"
    "request" = "requisicao"
    "requests" = "requisicoes"
    "response" = "resposta"
    "return" = "retornar"
    "retries" = "tentativas"
    "save" = "salvar"
    "schedule" = "agendar"
    "send" = "enviar"
    "service" = "servico"
    "should" = "deve"
    "slots" = "slots"
    "sqlite" = "sqlite"
    "status" = "status"
    "success" = "sucesso"
    "succeeds" = "sucesso"
    "test" = "teste"
    "tests" = "testes"
    "the" = ""
    "theory" = "teoria"
    "to" = "para"
    "token" = "token"
    "true" = "verdadeiro"
    "unauthorized" = "nao autorizado"
    "update" = "atualizar"
    "user" = "usuario"
    "users" = "usuarios"
    "valid" = "valido"
    "when" = "quando"
    "with" = "com"
    "without" = "sem"
}

function Split-Words {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return @()
    }

    $normalized = $Text -replace "_", " "
    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $normalized,
        "([A-Z]+(?=[A-Z][a-z]|[0-9]|$)|[A-Z]?[a-z]+|[0-9]+)"
    )

    $result = New-Object System.Collections.Generic.List[string]
    foreach ($m in $matches) {
        if (-not [string]::IsNullOrWhiteSpace($m.Value)) {
            [void]$result.Add($m.Value)
        }
    }

    return $result.ToArray()
}

function Convert-Phrase {
    param([string]$Raw)

    $tokens = Split-Words -Text $Raw
    $translated = New-Object System.Collections.Generic.List[string]

    foreach ($token in $tokens) {
        $key = $token.ToLowerInvariant()
        if ($wordMap.ContainsKey($key)) {
            $value = $wordMap[$key]
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                [void]$translated.Add($value)
            }
            continue
        }

        [void]$translated.Add($key)
    }

    $phrase = ($translated -join " ").Trim()
    $phrase = $phrase -replace "\s+", " "
    $phrase = $phrase -replace "\bnot found\b", "nao encontrado"
    $phrase = $phrase -replace "\bbad request\b", "requisicao invalida"
    $phrase = $phrase -replace "\bno content\b", "sem conteudo"

    return $phrase.Trim()
}

function To-Sentence {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $Text
    }

    if ($Text.Length -eq 1) {
        return $Text.ToUpperInvariant()
    }

    return $Text.Substring(0, 1).ToUpperInvariant() + $Text.Substring(1)
}

function Build-DisplayName {
    param(
        [string]$ClassName,
        [string]$MethodName
    )

    $classBase = ($ClassName -replace "Tests$", "" -replace "Test$", "")
    $classLabel = To-Sentence (Convert-Phrase -Raw $classBase)
    if ([string]::IsNullOrWhiteSpace($classLabel)) {
        $classLabel = $ClassName
    }

    $cleanMethodName = $MethodName -replace "Async", ""
    $pattern = "^(?<context>.+?)_Should(?<expected>.+?)(?:_When(?<condition>.+))?$"
    $m = [System.Text.RegularExpressions.Regex]::Match($cleanMethodName, $pattern)

    if ($m.Success) {
        $context = To-Sentence (Convert-Phrase -Raw $m.Groups["context"].Value)
        $expected = (Convert-Phrase -Raw $m.Groups["expected"].Value)
        $condition = (Convert-Phrase -Raw $m.Groups["condition"].Value)

        if ([string]::IsNullOrWhiteSpace($expected)) {
            $expected = "executar o comportamento esperado"
        }

        if ([string]::IsNullOrWhiteSpace($context)) {
            $context = "Cenario"
        }

        if ([string]::IsNullOrWhiteSpace($condition)) {
            return "$classLabel | $context | Deve $expected"
        }

        return "$classLabel | $context | Deve $expected quando $condition"
    }

    $fallback = To-Sentence (Convert-Phrase -Raw $cleanMethodName)
    if ([string]::IsNullOrWhiteSpace($fallback)) {
        $fallback = $MethodName
    }

    return "$classLabel | $fallback"
}

function Build-UpdatedAttributeLine {
    param(
        [string]$OriginalLine,
        [string]$AttributeName,
        [string]$DisplayName
    )

    $indent = ""
    $indentMatch = [System.Text.RegularExpressions.Regex]::Match($OriginalLine, "^(\s*)")
    if ($indentMatch.Success) {
        $indent = $indentMatch.Groups[1].Value
    }

    $displayEscaped = $DisplayName.Replace('"', '\"')
    $withArgsPattern = "^\s*\[$AttributeName\((?<args>.*)\)\]\s*$"
    $argsMatch = [System.Text.RegularExpressions.Regex]::Match($OriginalLine, $withArgsPattern)

    if ($argsMatch.Success) {
        $args = $argsMatch.Groups["args"].Value.Trim()
        if ([string]::IsNullOrWhiteSpace($args)) {
            return "$indent[$AttributeName(DisplayName = ""$displayEscaped"")]"
        }

        return "$indent[$AttributeName($args, DisplayName = ""$displayEscaped"")]"
    }

    return "$indent[$AttributeName(DisplayName = ""$displayEscaped"")]"
}

function Find-ClassName {
    param([string[]]$Lines)

    foreach ($line in $Lines) {
        $classMatch = [System.Text.RegularExpressions.Regex]::Match($line, "class\s+(?<name>[A-Za-z0-9_]+)")
        if ($classMatch.Success) {
            return $classMatch.Groups["name"].Value
        }
    }

    return "Testes"
}

function Find-MethodName {
    param(
        [string[]]$Lines,
        [int]$StartIndex
    )

    for ($i = $StartIndex; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i].Trim()
        if ($line.StartsWith("[") -or $line.StartsWith("//")) {
            continue
        }

        $methodMatch = [System.Text.RegularExpressions.Regex]::Match(
            $line,
            "^(public|private|internal|protected)\s+[\w\<\>\[\],\.\?\s]+\s+(?<name>[A-Za-z0-9_]+)\s*\("
        )

        if ($methodMatch.Success) {
            return $methodMatch.Groups["name"].Value
        }
    }

    return $null
}

$files = Get-ChildItem $RootPath -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch "\\bin\\" -and $_.FullName -notmatch "\\obj\\" }

$total = 0
$missing = 0
$changedFiles = 0

foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    $className = Find-ClassName -Lines $lines
    $changed = $false

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        $factMatch = [System.Text.RegularExpressions.Regex]::Match($line, "^\s*\[(?<name>Fact|Theory)(\((?<args>.*)\))?\]\s*$")
        if (-not $factMatch.Success) {
            continue
        }

        $attributeName = $factMatch.Groups["name"].Value
        $args = $factMatch.Groups["args"].Value
        $total++

        if ($args -match "DisplayName\s*=") {
            continue
        }

        $missing++
        if (-not $Apply) {
            continue
        }

        $methodName = Find-MethodName -Lines $lines -StartIndex ($i + 1)
        if ([string]::IsNullOrWhiteSpace($methodName)) {
            continue
        }

        $displayName = Build-DisplayName -ClassName $className -MethodName $methodName
        $lines[$i] = Build-UpdatedAttributeLine -OriginalLine $line -AttributeName $attributeName -DisplayName $displayName
        $changed = $true
    }

    if ($Apply -and $changed) {
        Set-Content -Path $file.FullName -Encoding utf8 -Value $lines
        $changedFiles++
    }
}

Write-Output "Total de testes encontrados: $total"
Write-Output "Testes sem DisplayName: $missing"
if ($Apply) {
    Write-Output "Arquivos alterados: $changedFiles"
}

if ($Check -and $missing -gt 0) {
    exit 1
}

exit 0

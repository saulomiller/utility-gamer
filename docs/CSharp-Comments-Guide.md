# Comentarios em C# - Padrao de Referencia

Este documento define o padrao de comentarios para o projeto `Utility Gamer`.

## Tipos de comentario

### 1) Linha unica (`//`)
Use para notas curtas e contexto local.

```csharp
// Tolerancia definida pelo time financeiro para arredondamento.
const double Tolerancia = 0.001;
```

### 2) Multiplas linhas (`/* */`)
Use apenas quando um bloco maior realmente for necessario.
Evite deixar codigo morto comentado por muito tempo.

```csharp
/*
  Bloco temporariamente desativado durante investigacao.
*/
```

### 3) Documentacao XML (`///`)
Use em classes, metodos, propriedades e interfaces publicas.
Essa documentacao alimenta IntelliSense e geracao de docs.

```csharp
/// <summary>
/// Calcula a soma de dois numeros inteiros.
/// </summary>
/// <param name="a">Primeiro operando.</param>
/// <param name="b">Segundo operando.</param>
/// <returns>Soma de <paramref name="a"/> e <paramref name="b"/>.</returns>
public int Somar(int a, int b) => checked(a + b);
```

## Boas praticas

- Explique o por que da decisao, nao o que o codigo obvio ja mostra.
- Mantenha comentarios sincronizados com o comportamento atual.
- Prefira comentarios curtos, objetivos e acionaveis.
- Use `<inheritdoc/>` quando a implementacao apenas repete contrato de interface/base.

## Evite

- Comentarios redundantes.
- Comentarios desatualizados.
- Codigo comentado permanente no repositorio.
- Excesso de comentario em codigo simples e autoexplicativo.

## Convencao adotada neste repositorio

- `//` para cabecalhos de secao internos e observacoes pontuais.
- `///` para membros publicos que representam contrato de uso.
- `/* */` somente quando o bloco precisa mesmo de contexto multi-linha.

## Configuracao opcional no `.csproj`

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```


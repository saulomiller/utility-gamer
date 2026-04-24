# Utility Gamer

Utility Gamer é um app desktop para Windows criado para automatizar ajustes do sistema quando um jogo inicia.

Ele foi pensado para cenários onde o jogo roda em uma resolução diferente da área de trabalho, ajudando a preparar o ambiente automaticamente e restaurar tudo após o jogo fechar.

---

# 🎯 Problema que ele resolve

Quando um jogo usa uma resolução diferente da do desktop, o Windows nem sempre entra no comportamento esperado (como `Independent Flip`).

Exemplo comum:

- Desktop: `1920x1080`
- Jogo: `1280x960`
- Resultado: comportamento gráfico inconsistente ou indesejado

O Utility Gamer automatiza os ajustes necessários antes e depois do jogo rodar.

---

# ⚙️ Como funciona

1. Você adiciona o executável do jogo.
2. O app monitora o processo em segundo plano.
3. Quando o jogo inicia:
   - aplica a resolução configurada
   - altera o plano de energia (opcional)
   - executa tweaks do sistema (opcional)
4. Quando o jogo fecha:
   - restaura a resolução original
   - restaura o plano de energia anterior

---

# 🧩 Recursos principais

## Troca automática de resolução

Usa a API `ChangeDisplaySettingsEx` para ajustar a resolução do monitor automaticamente.

---

## Seleção de monitor

Permite definir qual display será ajustado em setups com múltiplos monitores.

---

## Troca de plano de energia

Integra com `powercfg` para:

- ativar um plano específico ao iniciar o jogo
- restaurar o plano anterior ao fechar

---

## Tweaks opcionais do sistema

Inclui ajustes úteis para ambiente gamer:

- `GlobalTimerResolutionRequests = 1`
- desativar `Game Bar / DVR`
- remover `Xbox Gaming Overlay`
- desativar `Sticky Keys`
- desativar aceleração do mouse

---

## Bandeja do sistema

Permite:

- iniciar com o Windows
- rodar em segundo plano
- controlar monitoramento via tray

---


## 🛠️ Reparos rápidos

Atalhos visuais para execução de comandos de manutenção do Windows:

```bash
sfc /scannow
dism /Online /Cleanup-Image /RestoreHealth

#!/usr/bin/env python3
"""
Notas de versão a partir do CHANGELOG.md (usado pelo workflow de lançamento).

Formato esperado do CHANGELOG.md:

    # Histórico de versões

    ## [Não lançado]
    - O que já mudou e ainda não foi lançado

    ## [1.2.0] - 2026-10-15
    - Visual novo, no padrão da NokyLabs
    - Correção: ...

Uso:
    notas_da_versao.py ler 1.2.0 CHANGELOG.md
        Procura a seção "## [1.2.0]". Se não existir, usa a "## [Não lançado]".
        Grava no GITHUB_OUTPUT:  texto (as notas)  e  promover (sim/nao).

    notas_da_versao.py promover 1.2.0 CHANGELOG.md
        Transforma "## [Não lançado]" em "## [1.2.0] - data de hoje" e deixa
        uma seção "Não lançado" nova e vazia no topo.
"""

from __future__ import annotations

import datetime as dt
import os
import re
import sys

NAO_LANCADO = re.compile(r"^##\s*\[?\s*n[ãa]o\s+lan[çc]ado\s*\]?\s*$", re.IGNORECASE | re.MULTILINE)


def secao_da_versao(versao: str) -> re.Pattern:
    v = re.escape(versao.lstrip("vV"))
    return re.compile(rf"^##\s*\[?\s*v?{v}\s*\]?(?:\s*[-–—].*)?$", re.IGNORECASE | re.MULTILINE)


def conteudo_apos(texto: str, cabecalho: re.Match) -> str:
    """Texto entre o cabeçalho encontrado e o próximo '## '."""
    resto = texto[cabecalho.end():]
    proximo = re.search(r"^##\s", resto, re.MULTILINE)
    return (resto[: proximo.start()] if proximo else resto).strip()


def ler(versao: str, caminho: str) -> tuple[str, bool]:
    if not os.path.exists(caminho):
        print(f"::warning::Não achei o {caminho}: o lançamento sai sem notas de versão.")
        return "", False
    texto = open(caminho, encoding="utf-8").read()

    m = secao_da_versao(versao).search(texto)
    if m:
        notas = conteudo_apos(texto, m)
        if notas:
            return notas, False

    m = NAO_LANCADO.search(texto)
    if m:
        notas = conteudo_apos(texto, m)
        if notas:
            print(f"Usando a seção 'Não lançado' como notas da versão {versao}.")
            return notas, True

    print(f"::warning::O CHANGELOG não tem a seção da versão {versao} nem itens em 'Não lançado'. "
          f"O lançamento sai sem notas de versão.")
    return "", False


def promover(versao: str, caminho: str) -> bool:
    texto = open(caminho, encoding="utf-8").read()
    m = NAO_LANCADO.search(texto)
    if not m or secao_da_versao(versao).search(texto):
        return False
    hoje = dt.date.today().isoformat()
    novo = texto[: m.start()] + f"## [Não lançado]\n\n## [{versao.lstrip('vV')}] - {hoje}" + texto[m.end():]
    open(caminho, "w", encoding="utf-8", newline="\n").write(novo)
    return True


def main() -> int:
    if len(sys.argv) < 4 or sys.argv[1] not in ("ler", "promover"):
        print(__doc__)
        return 2
    acao, versao, caminho = sys.argv[1], sys.argv[2], sys.argv[3]

    if acao == "promover":
        print("CHANGELOG atualizado." if promover(versao, caminho) else "Nada a promover no CHANGELOG.")
        return 0

    notas, deve_promover = ler(versao, caminho)
    print(notas or "(sem notas)")
    saida = os.environ.get("GITHUB_OUTPUT")
    if saida:
        with open(saida, "a", encoding="utf-8") as f:
            f.write("texto<<FIM_DAS_NOTAS\n")
            f.write((notas or "Melhorias e correções.") + "\n")
            f.write("FIM_DAS_NOTAS\n")
            f.write(f"promover={'sim' if deve_promover else 'nao'}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())

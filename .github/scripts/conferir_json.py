"""Confere se um catalogo.json baixado é válido antes de embuti-lo no launcher."""
import json
import sys

try:
    with open(sys.argv[1], encoding="utf-8") as f:
        catalogo = json.load(f)
    assert isinstance(catalogo.get("apps"), list) and catalogo["apps"], "sem lista de apps"
except Exception as e:  # qualquer problema = usar a cópia do repositório
    print(f"Catálogo inválido: {e}")
    sys.exit(1)
print(f"Catálogo ok ({len(catalogo['apps'])} apps).")

# Verilog Language Extension documentation

This directory contains the Sphinx/Read the Docs source for the project.

Read the Docs uses `../.readthedocs.yaml`, which points Sphinx at
`docs/conf.py` and installs dependencies from `docs/requirements.txt`.

To build locally in a Python virtual environment:

```powershell
py -m venv .venv-docs
.\.venv-docs\Scripts\Activate.ps1
python -m pip install -r docs\requirements.txt
sphinx-build -W --keep-going -b html docs docs\_build\html
```

Open `docs\_build\html\index.html` after a successful build.

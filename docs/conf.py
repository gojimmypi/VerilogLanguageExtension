from pathlib import Path
import os
import xml.etree.ElementTree as ET

PROJECT_ROOT = Path(__file__).resolve().parents[1]

project = "Verilog Language Extension"
author = "gojimmypi and contributors"
copyright = "2019-2026, gojimmypi and contributors"


def _read_release_version():
    candidates = (
        PROJECT_ROOT / "source.extension.vsixmanifest",
        PROJECT_ROOT / "bin" / "Release" / "extension.vsixmanifest",
    )

    for candidate in candidates:
        if not candidate.is_file():
            continue

        try:
            root = ET.parse(candidate).getroot()
            identity = next(
                node for node in root.iter()
                if node.tag.rsplit("}", 1)[-1] == "Identity"
            )
            value = identity.attrib.get("Version", "").strip()
            if value:
                return value
        except (ET.ParseError, OSError, StopIteration):
            pass

    rtd_version = os.environ.get("READTHEDOCS_VERSION_NAME", "").strip()
    if rtd_version and rtd_version not in ("latest", "stable"):
        return rtd_version

    return "development"


release = _read_release_version()
version = release

extensions = []

templates_path = []
exclude_patterns = ["_build", "Thumbs.db", ".DS_Store"]

html_theme = "sphinx_rtd_theme"
html_static_path = ["_static"]
html_css_files = ["custom.css"]
html_title = "Verilog Language Extension"
html_theme_options = {
    "collapse_navigation": False,
    "navigation_depth": 4,
    "sticky_navigation": True,
    "titles_only": False,
}

pygments_style = "sphinx"

rst_epilog = """
.. |repo| replace:: VerilogLanguageExtension
"""

import pathlib,base64
root=pathlib.Path(__file__).resolve().parents[2]
template=(root/'Tools/Bruno/preview-template.html').read_text()
bundle=(root/'Tools/Bruno/preview.bundle.js').read_text().replace('</script','<\\/script')
bundle='\n'.join(line.rstrip() for line in bundle.splitlines())
glb=base64.b64encode((root/'Documentation/Bruno/Bruno.glb').read_bytes()).decode()
tool_glb=base64.b64encode((root/'Documentation/Bruno/BrunoTools.glb').read_bytes()).decode()
(root/'Documentation/Bruno/Bruno-Preview.html').write_text(template.replace('__GLB__',glb).replace('__BUNDLE__',bundle).replace('__TOOLS_GLB__',tool_glb))

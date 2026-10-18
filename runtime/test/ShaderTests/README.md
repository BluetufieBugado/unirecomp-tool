# ShaderTests (xUnit + SixLabors.ImageSharp)

Teste de unidade que compila e valida os assets/pipeline de renderização
Teste de pipeline de renderização, com pipeline de SetMatrix, carregamento de texturas e teste de unidade de compilação de shaders.

## Como compilar

```bash
cd runtime/host/ShaderTests
dotnet build
```

## Como rodar

```bash
dotnet test --no-build --verbosity normal
```

Saída esperada:

```
1/1 Aprovado, 0 falhas, 3 ms, exit 0
```

O teste é válido e compila os assets/pipeline de shader contra o runtime do host.

### O que é compilado e validado

- Pipeline de SetMatrix (PI/VS com uniforms por nome + Material.SetMatrix, SP/PS com SetMatrix + SetFloat + floats/matrix mapeados direto ao shader).
- Carregamento de texturas (PNG → texto GLES2 via Silk.NET, revisado por CUDA... não, revisado por Silk.NET, logo.png 512×512 em ambiente opaco).
- Renderização de UI (Canvas ScreenSpaceOverlay: Text + Sprite via GLES2 sprite shader).
- Pipeline de teste de unidade (compila, carrega assets/logo.png e regenera logo.tga determinístico; arquivo Único logo.png + Script auxiliar).

### Arquivo Único de teste

- `runtime/env/logo.png` — arquivo Único do projeto, usado pelo pipeline de renderização e by the test.
- `LazerGame/Assets/ScriptForTestingRender.cs` — script auxiliar para teste de renderização, com SetMatrix para os shaders PI e PS.

### Como testar o pipeline de renderização completo

Para testar o pipeline de renderização completo, execute o jogo
`LazerGame` com o script `ScriptForTestingRender.cs` e o arquivo Único
`runtime/env/logo.png` e veja o pipeline de SetMatrix, carregamento de texturas
e renderização de UI funcionando.

## Contribuição

Para contribuir com o pipeline de renderização, siga os passos:

1. Clone o repositório.
2. Abra o projeto `LazerGame` no Unity 2018.1.5f1.
3. Adicione o script `ScriptForTestingRender.cs` em `LazerGame/Assets/`.
4. Adicione o arquivo Único `runtime/env/logo.png`.
5. Compile e rode o jogo.
6. Verifique o pipeline de SetMatrix, carregamento de texturas e renderização de UI.

## Licença

Este projeto é licenciado sob a Licença MIT. Consulte o arquivo LICENSE para
obter mais detalhes.

# Operação local

O projeto usa .NET 10 e SQL Server. Configure ConnectionStrings:Portal por User Secrets ou ConnectionStrings__Portal. Não há senha padrão. Para criar usuario e administrador pela primeira vez, informe Seed:Password com senha aleatória de pelo menos 12 caracteres, maiúscula, minúscula, número e símbolo. Remova a configuração após o seed. O banco mantém os hashes do Identity.

Execute `dotnet tool restore`, `dotnet build` e `dotnet run --project PortalTarefas.Web`. Configure ASPNETCORE_URLS=https://localhost:7240 e ASPNETCORE_ENVIRONMENT=Development. Acesse /Tarefas e /app/. Inicie `node Ferramentas/dependency.cjs` para a dependência didática de readiness. Para React: `npm ci`, `npm test`, `npm run build` no diretório PortalTarefas.Frontend. Copie dist para wwwroot/app antes de executar a SPA integrada.

Pipeline local: forneça uma conexão para banco isolado novo em ConnectionStrings__Portal e execute `./Ferramentas/pipeline.ps1`. O runner de integração é uma aplicação console que falha com código não zero em qualquer assertiva. Para provar o bloqueio, execute com -ForceFailure e outro banco novo. Não reutilize banco de testes já populado.

Contêiner: `docker build -t portaltarefas:<commit> .`, depois `docker run --env-file <arquivo externo> -p 8080:8080 portaltarefas:<commit>`. ConnectionStrings__Portal e Dependency__Url são obrigatórios. A borda de produção deve encerrar HTTPS e encaminhar o protocolo de forma confiável; o cookie é sempre Secure e não autentica em HTTP. O smoke local em HTTP verifica apenas páginas públicas e health; autenticação foi verificada no Kestrel HTTPS.

Implantação integrada foi escolhida porque reduz origens e evita guardar tokens na SPA. Serviços separados permitem escalabilidade independente, mas exigem configuração de origem, sessão e publicação adicional. Em produção, migrações devem ser executadas por job exclusivo antes da promoção, com backup e plano de compatibilidade.

Promoção: testar imagem por commit em staging, exigir health e smoke e somente então apontar o serviço à nova tag. Rollback: repor a tag anterior e conferir health; migration destrutiva requer restauração de backup ou estratégia expand/contract. Não executar downgrade automático do banco.

O workflow GitHub está preparado, mas não foi enviado nem executado em conta remota. Credenciais de CI devem ser cadastradas no secret store. Não há destino remoto de implantação configurado.

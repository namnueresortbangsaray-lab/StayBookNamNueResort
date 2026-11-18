# Stay Book — common tasks. Docker targets run inside the "web" container; others need the .NET 10 SDK and Node 22.
COMPOSE ?= docker compose
CLI = $(COMPOSE) exec web dotnet StayBook.dll
NAME ?= NewMigration

.PHONY: up down logs shell prod-up dev migrate seed user-create backup import-php \
        build assets test format format-check smoke migrations-add

up:            ## Build and start (http://localhost:8080)
	$(COMPOSE) up -d --build
down:
	$(COMPOSE) down
logs:
	$(COMPOSE) logs -f web
shell:
	$(COMPOSE) exec web bash
prod-up:       ## Nginx + Kestrel
	$(COMPOSE) -f docker-compose.yml -f docker-compose.prod.yml up -d --build
dev:           ## dotnet watch + Tailwind watcher on http://localhost:8082
	$(COMPOSE) --profile dev up web-dev assets-dev

migrate:
	$(CLI) migrate
seed:
	$(CLI) seed
user-create:   ## make user-create NAME_ARG="..." EMAIL=... PASSWORD=... ROLE=front_desk
	$(CLI) user:create --name="$(NAME_ARG)" --email=$(EMAIL) --password=$(PASSWORD) --role=$(or $(ROLE),front_desk)
backup:
	$(CLI) db:backup
import-php:    ## Import the v1 database and uploads from the reused volumes
	$(CLI) import:php --sqlite=/app/storage/database/stay_book.sqlite --uploads=/app/storage/uploads --truncate

assets:
	npm ci && npm run build
build: assets
	dotnet build StayBook.sln -c Release
test:
	dotnet test StayBook.sln
format:
	dotnet format StayBook.sln
format-check:
	dotnet format StayBook.sln --verify-no-changes
smoke:
	sh tests/smoke/smoke-test.sh

migrations-add: ## make migrations-add NAME=AddSomething
	dotnet ef migrations add $(NAME) --project StayBook.csproj --output-dir Data/Migrations

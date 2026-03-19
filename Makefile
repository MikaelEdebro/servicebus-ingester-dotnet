include .env
export

PROJECT := src/ServiceBusIngester/ServiceBusIngester.csproj

.PHONY: run build clean restore migrate migrate-down docker-build push deploy

run:
	dotnet run --project $(PROJECT)

build:
	dotnet build $(PROJECT)

clean:
	dotnet clean $(PROJECT)

restore:
	dotnet restore $(PROJECT)

DATABASE_URL := postgres://$(DB_USER):$(DB_PASSWORD)@$(DB_HOST):$(DB_PORT)/$(DB_DATABASE)?sslmode=$(DB_SSL_MODE)

migrate:
	dbmate -d db/migrations --url "$(DATABASE_URL)" up

migrate-down:
	dbmate -d db/migrations --url "$(DATABASE_URL)" down

ACR := acrvcedcsp.azurecr.io
IMAGE := $(ACR)/experiment/ingestion/servicebus-ingester-dotnet
TAG := $(shell date +%Y%m%d.%H%M%S)

docker-build:
	docker build --platform linux/amd64 -t $(IMAGE):$(TAG) .

push: docker-build
	az acr login --name acrvcedcsp
	docker push $(IMAGE):$(TAG)
	@echo "\nPushed: $(IMAGE):$(TAG)"

deploy: push
	sed -i '' 's|image: $(IMAGE):.*|image: $(IMAGE):$(TAG)|' deploy/dev/deployment.yaml
	git add deploy/dev/deployment.yaml
	git commit -m "deploy: $(TAG)"
	git push

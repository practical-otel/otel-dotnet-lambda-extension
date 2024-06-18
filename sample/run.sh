#!/bin/sh
if [ ! -e "environment.json" ]; then
    echo "environment.json file doesn't exist"
    exit 1
fi

echo "Starting the collector..." # the function will 502 if the collector is down!
docker-compose up -d

echo "Building..."
sam build

echo "Running lambda simulator..."
sam local start-api --env-vars environment.json --docker-network sample_collector_net --skip-pull-image

echo "Go to http_tests/questions.http in VSCode to click on some requests to try"

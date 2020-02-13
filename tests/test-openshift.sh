#!/bin/bash
set -e

# https://docs.okd.io/latest/minishift/getting-started/installing.html
# https://github.com/minishift/minishift/releases

project=myproject

echo "-------- Minishift setup and login --------"

# verify that minishift and oc is installed
minishift version > /dev/null
oc version > /dev/null
ip=$(minishift ip)

# download admin kube.config from minishift
minishift ssh -- cat .kube/config > ./minishift.conf

# add image stream to test with
oc create -n myproject -f "test-imagestreams.json" --kubeconfig=./minishift.conf

# expose registry and retrive ip,port and credentials
oc expose dc docker-registry --type=NodePort --name=docker-registry-ingress -n default --kubeconfig=./minishift.conf
nodeport=$(oc get svc docker-registry-ingress -n default -o go-template='{{range.spec.ports}}{{if .nodePort}}{{.nodePort}}{{"\n"}}{{end}}{{end}}' --kubeconfig=./minishift.conf)
echo "minishift registry: $ip:$nodeport"

oc login https://$ip:8443 -u developer -p test --kubeconfig=./minishift-dev.conf --insecure-skip-tls-verify > /dev/null
devtoken=$(oc whoami -t --kubeconfig=./minishift-dev.conf)
echo "dev token: $devtoken"

echo "-------- Build nibbler --------"

dotnet build ../Nibbler -o ./tmp-nibbler-build -f netcoreapp3.1

echo "-------- Run nibbler --------"

dotnet ./tmp-nibbler-build/nibbler.dll \
	--base-image $ip:$nodeport/$project/test-dotnet:3.1 \
	--username developer \
	--password $devtoken \
	--destination $ip:$nodeport/$project/nibbler-test:latest \
	--workdir /app \
	--cmd "dotnet TestData.dll" \
	--insecure \
	-v

#### Cleanup

##oc --kubeconfig=./minishift.conf -n default get all

#oc delete svc docker-registry-ingress -n default --kubeconfig=./minishift.conf
#oc delete -n myproject -f "test-imagestreams.json" --kubeconfig=./minishift.conf

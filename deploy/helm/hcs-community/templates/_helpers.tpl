{{- define "hcs.name" -}}hcs-community{{- end -}}
{{- define "hcs.labels" -}}
app.kubernetes.io/part-of: hcs-community
app.kubernetes.io/managed-by: Helm
{{- end -}}

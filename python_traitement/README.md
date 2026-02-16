# Description 

Python program handling the request from the frontend and linking it to the back.
Process logs files before sending them to the back.

# Developpement

First, start the downstream server then this python one 
poetry run uvicorn main:app --reload --host 127.0.0.1 --port 8000

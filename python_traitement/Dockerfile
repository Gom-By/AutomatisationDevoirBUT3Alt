FROM python:3.12-alpine

WORKDIR /app

COPY . ./

RUN pip install poetry

RUN poetry install --no-root

EXPOSE 8000

CMD ["poetry", "run", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]

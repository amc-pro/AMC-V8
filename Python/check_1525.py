import pandas as pd

df = pd.read_csv('/home/ubuntu/AMC-V8/tests_and_data/historical_data/NQ_1min.csv', skiprows=2, header=None)
df.columns = ['Datetime', 'Close', 'High', 'Low', 'Open', 'Volume']
sub = df[df['Datetime'].str.contains('2026-08-19') & (df['Datetime'].str.contains('15:2') | df['Datetime'].str.contains('15:30'))]
print(sub)
